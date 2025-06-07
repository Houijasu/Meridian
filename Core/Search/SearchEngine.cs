namespace Meridian.Core.Search;

using System.Runtime.CompilerServices;
using System.Text;

using Evaluation;

using MoveGeneration;

/// <summary>
///    The main search engine using alpha-beta pruning with quiescence search.
/// </summary>
public class SearchEngine(int ttSizeMB = 128)
{
   private readonly Move[] moveBuffer = new Move[SearchConstants.MaxPly * 256];
   private readonly ScoredMove[] scoredMoveBuffer = new ScoredMove[SearchConstants.MaxPly * 256];
   private readonly SearchInfo searchInfo = new();
   private readonly TranspositionTable tt = new(ttSizeMB);
   private readonly MoveOrdering moveOrdering = new();
   private readonly StringBuilder uciStringBuilder = new(256); // For UCI output

   private static readonly int[,] LMRTable = new int[64, 64];
   private static readonly int[] FutilityMargins = new int[SearchConstants.FutilityMaxDepth + 1];

   static SearchEngine()
   {
      for (int depth = 1; depth < 64; depth++)
      {
         for (int moves = 1; moves < 64; moves++)
         {
            LMRTable[depth, moves] = (int)(SearchConstants.LMRBase + Math.Log(depth) * Math.Log(moves) / SearchConstants.LMRFactor);
         }
      }
      
      for (int depth = 0; depth <= SearchConstants.FutilityMaxDepth; depth++)
      {
         FutilityMargins[depth] = SearchConstants.FutilityMarginBase * depth;
      }
   }

   /// <summary>
   ///    Clears the transposition table.
   /// </summary>
   public void ClearTT() => tt.Clear();

   /// <summary>
   ///    Clears the move ordering tables.
   /// </summary>
   public void ClearMoveOrdering() => moveOrdering.Clear();

   /// <summary>
   ///    Stops the current search.
   /// </summary>
   public void StopSearch() => searchInfo.ShouldStop = true;

   /// <summary>
   ///    Gets the current best move without searching.
   /// </summary>
   public Move GetBestMove() => searchInfo.BestMove;
   
   /// <summary>
   ///    Gets the ponder move (expected opponent response).
   /// </summary>
   public Move GetPonderMove() => searchInfo.PonderMove;

   /// <summary>
   ///    Searches for the best move in the given position.
   /// </summary>
   public Move Search(Position position, int maxDepth, int maxTime = int.MaxValue, CancellationToken cancellationToken = default)
   {
      searchInfo.Nodes = 0;
      searchInfo.MaxDepth = Math.Min(maxDepth, SearchConstants.MaxDepth);
      searchInfo.MaxTime = maxTime;
      searchInfo.ShouldStop = false;
      searchInfo.BestMove = Move.Null;
      searchInfo.BestScore = -SearchConstants.Infinity;
      searchInfo.CancellationToken = cancellationToken;
      searchInfo.Timer.Restart();

      var aspirationDelta = 50;
      var alpha = -SearchConstants.Infinity;
      var beta = SearchConstants.Infinity;

      // Before starting search, ensure we can at least report something
      searchInfo.Nodes = 1; // Ensure at least 1 node for GUI
      
      for (var depth = 1; depth <= searchInfo.MaxDepth; depth++)
      {
         if (searchInfo.ShouldStop || cancellationToken.IsCancellationRequested) 
         {
            searchInfo.ShouldStop = true;
            break;
         }
         
         searchInfo.CurrentDepth = depth;
         
         if (depth >= 4 && searchInfo.BestScore is > -SearchConstants.CheckmateThreshold and < SearchConstants.CheckmateThreshold)
         {
            alpha = searchInfo.BestScore - aspirationDelta;
            beta = searchInfo.BestScore + aspirationDelta;
         }
         
         var score = AlphaBeta(position, depth, alpha, beta, 0);
         
         if (!searchInfo.ShouldStop)
         {
            if (score <= alpha || score >= beta)
            {
               //aspirationDelta = Math.Min(aspirationDelta * 2, 500);
               alpha = -SearchConstants.Infinity;
               beta = SearchConstants.Infinity;
               score = AlphaBeta(position, depth, alpha, beta, 0);
            }
            
            searchInfo.BestScore = score;
            aspirationDelta = 50;
            
            // Always print info for completed depths
            // Even if no move found (e.g., checkmate), we should report the score
            PrintSearchInfo();
         }
      }

      searchInfo.Timer.Stop();
      
      // Always output final info if we completed at least one depth
      // Even if no move found (e.g., checkmate), we should report the score
      if (searchInfo.CurrentDepth > 0)
      {
         PrintSearchInfo();
      }
      
      // Extract ponder move from PV
      if (searchInfo.BestMove != Move.Null)
      {
         ExtractPonderMove();
      }
      
      // Safety check: if we have no best move after searching, try to find any legal move
      if (searchInfo.BestMove.IsNull && searchInfo.CurrentDepth > 0)
      {
         var moveListSpan = moveBuffer.AsSpan(0, 256);
         var moveList = new MoveList(moveListSpan);
         MoveGenerator.GenerateMoves(in position, ref moveList);
         
         for (int i = 0; i < moveList.Count; i++)
         {
            var move = moveList.Moves[i];
            var testPos = position;
            testPos.MakeMove(move);
            
            if (!AttackDetection.IsKingInCheck(in testPos, position.SideToMove))
            {
               searchInfo.BestMove = move;
               break;
            }
         }
      }
      
      return searchInfo.BestMove;
   }

   /// <summary>
   ///    Alpha-beta search with fail-hard cutoffs.
   /// </summary>
   private int AlphaBeta(in Position position, int depth, int alpha, int beta, int ply, bool allowNull = true, Square lastCaptureSquare = Square.None)
   {
      searchInfo.Nodes++;
      
      // Clear PV length for this ply
      searchInfo.PvLength[ply] = 0;

      if ((searchInfo.Nodes & 63) == 0) // Check even more frequently (every 64 nodes)
      {
         if (searchInfo.ShouldStop) return 0;
         if (searchInfo.CancellationToken.IsCancellationRequested)
         {
            searchInfo.ShouldStop = true;
            return 0;
         }
         searchInfo.CheckTime();
         if (searchInfo.ShouldStop) return 0;
      }

      if (ply > 0 && IsDraw(in position))
         return SearchConstants.DrawScore;

      var originalAlpha = alpha;
      alpha = Math.Max(alpha, -SearchConstants.Checkmate + ply);
      beta = Math.Min(beta, SearchConstants.Checkmate - ply);
      if (alpha >= beta) return alpha;

      var inCheck = AttackDetection.IsKingInCheck(in position, position.SideToMove);
      
      var ttMove = Move.Null;

      if (tt.Probe(position.Hash, out var ttEntry))
      {
         ttMove = ttEntry.BestMove;
         
         // Validate TT move
         if (ttMove != Move.Null && !MoveGeneratorHelpers.IsMovePseudoLegal(in position, ttMove))
         {
            ttMove = Move.Null;
         }

         if (ttEntry.Depth >= depth && ply > 0) // Don't use TT cutoffs at root
         {
            var ttScore = TranspositionTable.ScoreFromTT(ttEntry.Score, ply);

            switch (ttEntry.Bound)
            {
               case BoundType.Exact:
                  return ttScore;

               case BoundType.Lower:
                  alpha = Math.Max(alpha, ttScore);
                  break;

               case BoundType.Upper:
                  beta = Math.Min(beta, ttScore);
                  break;
            }

            if (alpha >= beta)
               return ttScore;
         }
      }

      // Extensions
      var extensions = 0;
      
      // Check extension: extend search when in check
      if (inCheck)
      {
         extensions += SearchConstants.CheckExtension;
      }
      
      // Apply extensions
      var newDepth = depth + extensions;
      
      if (newDepth <= 0)
         return Quiescence(in position, alpha, beta, ply);

      if (allowNull && !inCheck && newDepth >= 3 && ply > 0 && HasNonPawnMaterial(in position))
      {
         var eval = Evaluator.Evaluate(in position);
         
         if (eval >= beta)
         {
            const int R = 3;
            var nullDepth = newDepth - R - 1;
            
            var nullPosition = position;
            nullPosition.SideToMove = nullPosition.SideToMove.Flip();
            nullPosition.EnPassantSquare = Square.None;
            nullPosition.Hash ^= Zobrist.GetSideKey(Color.Black);
            if (position.EnPassantSquare != Square.None)
               nullPosition.Hash ^= Zobrist.GetEnPassantKey(position.EnPassantSquare);
            
            var nullScore = -AlphaBeta(in nullPosition, nullDepth, -beta, -beta + 1, ply + 1, false);
            
            if (searchInfo.ShouldStop) return 0;
            
            if (nullScore >= beta)
            {
               if (nullScore >= SearchConstants.Checkmate - SearchConstants.MaxPly)
                  nullScore = beta;
                  
               return nullScore;
            }
            
            // Null move threat detection
            // If null move fails low by a large margin, we might be under threat
            if (nullScore < beta - 200 && depth >= SearchConstants.NullMoveThreatMinDepth)
            {
               extensions = SearchConstants.NullMoveThreatExtension;
               newDepth = depth + extensions;
            }
         }
      }

      var canUseFutility = !inCheck && 
                           newDepth <= SearchConstants.FutilityMaxDepth && 
                           Math.Abs(alpha) < SearchConstants.CheckmateThreshold &&
                           Math.Abs(beta) < SearchConstants.CheckmateThreshold;
      
      var staticEval = 0;
      var improving = false;
      if (!inCheck)
      {
         staticEval = Evaluator.Evaluate(in position);
         
         // Check if position is improving (used for LMP)
         // Use originalAlpha to avoid issues with mate distance pruning adjustments
         improving = staticEval > originalAlpha - 50;
         
         // Razoring: If static evaluation is far below alpha, we can prune
         if (beta - alpha == 1 && // Not a PV node
             newDepth <= SearchConstants.RazoringMaxDepth &&
             Math.Abs(alpha) < SearchConstants.CheckmateThreshold)
         {
            var razoringMargin = SearchConstants.RazoringMarginBase + 
                                 SearchConstants.RazoringMarginPerDepth * newDepth;
            
            if (staticEval + razoringMargin < alpha)
            {
               // Do a quiescence search to verify the position is really bad
               var razoringScore = Quiescence(in position, alpha - razoringMargin, alpha - razoringMargin + 1, ply);
               
               if (razoringScore <= alpha - razoringMargin)
                  return razoringScore;
            }
         }
         
         // Probcut: Try to prove a beta cutoff with reduced depth search
         if (beta - alpha == 1 && // Not a PV node
             !allowNull &&
             newDepth >= SearchConstants.ProbcutMinDepth &&
             Math.Abs(beta) < SearchConstants.CheckmateThreshold &&
             staticEval >= beta + SearchConstants.ProbcutMargin)
         {
            var probcutBeta = beta + SearchConstants.ProbcutMargin;
            var probcutDepth = newDepth - SearchConstants.ProbcutDepthReduction;
            
            // Generate only capture moves for probcut
            var probcutMoveListSpan = moveBuffer.AsSpan(ply * 256, 256);
            var probcutMoveList = new MoveList(probcutMoveListSpan);
            MoveGenerator.GenerateMoves(in position, ref probcutMoveList);
            
            // Try captures that might prove beta cutoff
            for (int i = 0; i < probcutMoveList.Count; i++)
            {
               var move = probcutMoveList.Moves[i];
               
               // Only try good captures
               if (!move.IsCapture || 
                   !StaticExchangeEvaluation.SeeGreaterOrEqual(in position, move, 0))
                  continue;
               
               var probcutPosition = position;
               probcutPosition.MakeMove(move);
               
               if (AttackDetection.IsKingInCheck(in probcutPosition, position.SideToMove))
                  continue;
               
               var probcutScore = -AlphaBeta(in probcutPosition, probcutDepth - 1, -probcutBeta, -probcutBeta + 1, ply + 1, false, move.IsCapture ? move.To : Square.None);
               
               if (searchInfo.ShouldStop) return 0;
               
               if (probcutScore >= probcutBeta)
                  return probcutScore;
            }
         }
      }
      
      // Internal Iterative Deepening (IID)
      // If we don't have a TT move and depth is sufficient, do a shallow search
      if (ttMove == Move.Null && 
          newDepth >= SearchConstants.IIDMinDepth &&
          (beta - alpha > 1 || newDepth >= SearchConstants.IIDMinDepth + 2)) // More aggressive in PV nodes
      {
         var iidDepth = newDepth - SearchConstants.IIDDepthReduction;
         var iidScore = AlphaBeta(in position, iidDepth, alpha, beta, ply, false, lastCaptureSquare);
         
         // Try to get a move from TT after IID search
         if (tt.Probe(position.Hash, out var iidEntry))
         {
            ttMove = iidEntry.BestMove;
         }
      }

      var moveListSpan = moveBuffer.AsSpan(ply * 256, 256);
      var moveList = new MoveList(moveListSpan);
      MoveGenerator.GenerateMoves(in position, ref moveList);

      if (moveList.Count == 0)
      {
         return inCheck
            ? -SearchConstants.Checkmate + ply
            : SearchConstants.DrawScore;
      }

      var scoredMovesSpan = scoredMoveBuffer.AsSpan(ply * 256, moveList.Count);
      moveOrdering.ScoreMoves(moveList.Moves, scoredMovesSpan, moveList.Count, ttMove, ply, in position);
      MoveOrdering.SortMoves(scoredMovesSpan, moveList.Count);

      var bestMove = Move.Null;
      var bestScore = -SearchConstants.Infinity;
      var searchedAnyMove = false;
      var movesSearched = 0;

      if (ttMove != Move.Null)
      {
         var newPosition = position;
         newPosition.MakeMove(ttMove);

         if (!AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
         {
            searchedAnyMove = true;
            movesSearched++;
            
            // Singular extension for TT move
            var ttExtension = 0;
            if (newDepth >= SearchConstants.SingularExtensionMinDepth &&
                !inCheck &&
                tt.Probe(position.Hash, out var singularEntry) &&
                singularEntry.Bound != BoundType.Upper &&
                singularEntry.Depth >= newDepth - 3)
            {
               var singularBeta = TranspositionTable.ScoreFromTT(singularEntry.Score, ply) - SearchConstants.SingularExtensionMargin;
               var singularDepth = newDepth - SearchConstants.SingularExtensionDepthReduction - 1;
               
               // Search other moves with reduced window
               var singularScore = AlphaBetaSingular(in position, singularDepth, singularBeta - 1, singularBeta, ply, ttMove);
               
               if (singularScore < singularBeta)
               {
                  ttExtension = 1;
               }
            }
            
            var searchDepth = Math.Min(newDepth - 1 + ttExtension, SearchConstants.MaxDepth - 1);
            var score = -AlphaBeta(in newPosition, searchDepth, -beta, -alpha, ply + 1, true, ttMove.IsCapture ? ttMove.To : Square.None);

            if (searchInfo.ShouldStop) return 0;

            if (score > bestScore)
            {
               bestScore = score;
               bestMove = ttMove;

               if (score > alpha)
               {
                  alpha = score;
                  UpdatePV(ply, ttMove);

                  if (ply == 0)
                  {
                     searchInfo.BestMove = ttMove;
                  }

                  if (score >= beta)
                  {
                     tt.Store(position.Hash, bestMove, TranspositionTable.ScoreToTT((short)bestScore, ply),
                        (byte)newDepth, BoundType.Lower);

                     return bestScore;
                  }
               }
               else if (ply == 0)
               {
                  // At root, always update best move and PV even if score doesn't improve alpha
                  // This ensures we have a move to play even in losing positions
                  searchInfo.BestMove = ttMove;
                  UpdatePV(ply, ttMove);
               }
            }
         }
      }
      
      for (var i = 0; i < moveList.Count; i++)
      {
         var move = scoredMovesSpan[i].Move;

         if (move.Equals(ttMove))
            continue;

         var newPosition = position;
         newPosition.MakeMove(move);

         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;

         searchedAnyMove = true;
         
         if (canUseFutility && 
             move is { IsCapture: false, IsPromotion: false } &&
             movesSearched >= 1 &&
             staticEval + FutilityMargins[newDepth] <= alpha)
         {
            continue;
         }
         
         // SEE pruning: skip bad captures in non-PV nodes
         if (move.IsCapture && 
             newDepth < 4 && 
             !inCheck &&
             movesSearched > 0 &&
             !StaticExchangeEvaluation.SeeGreaterOrEqual(in position, move, 0))
         {
            continue;
         }
         
         // Late Move Pruning: skip quiet moves late in move list at shallow depths
         if (!inCheck &&
             newDepth <= SearchConstants.LMPMaxDepth &&
             movesSearched >= SearchConstants.LMPMoveCount[newDepth] + (improving ? SearchConstants.LMPImprovingBonus : 0) &&
             move is { IsCapture: false, IsPromotion: false } &&
             bestScore > -SearchConstants.CheckmateThreshold)
         {
            continue;
         }
         
         // Increment movesSearched after all pruning decisions
         movesSearched++;
         
         int score;
         var reduction = 0;
         var extension = 0;
         
         // Recapture extension: extend when capturing back on the same square
         if (move.IsCapture && move.To == lastCaptureSquare)
         {
            extension += SearchConstants.RecaptureExtension;
         }
         
         // Passed pawn extension: extend passed pawns pushing to 7th rank
         if (IsPassedPawnPush7th(in position, move))
         {
            extension += SearchConstants.PassedPawnExtension;
         }
         
         if (newDepth >= SearchConstants.LMRMinDepth && 
             movesSearched > SearchConstants.LMRMinMoves &&
             !inCheck &&
             beta - alpha > 1 &&  // Not in PV node
             move is { IsCapture: false, IsPromotion: false })
         {
            reduction = LMRTable[Math.Min(newDepth, 63), Math.Min(movesSearched, 63)];
            
            // Reduce less for moves that give check
            var givesCheck = AttackDetection.IsKingInCheck(in newPosition, newPosition.SideToMove);
            if (givesCheck)
               reduction = Math.Max(reduction - 1, 0);
            
            // Reduce less for moves with good history
            var historyScore = moveOrdering.GetHistoryScore(move);
            if (historyScore > 0)
               reduction = Math.Max(reduction - 1, 0);
            else if (historyScore < -5000)
               reduction += 1;
            
            // Ensure we don't reduce too much
            reduction = Math.Min(newDepth - 2, Math.Max(reduction, 1));
         }
         
         if (movesSearched == 1)
         {
            var searchDepth = Math.Min(newDepth - 1 + extension, SearchConstants.MaxDepth - 1);
            score = -AlphaBeta(in newPosition, searchDepth, -beta, -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
         }
         else
         {
            if (reduction > 0)
            {
               var searchDepth = Math.Min(newDepth - reduction - 1 + extension, SearchConstants.MaxDepth - 1);
               score = -AlphaBeta(in newPosition, searchDepth, -(alpha + 1), -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
            }
            else
            {
               var searchDepth = Math.Min(newDepth - 1 + extension, SearchConstants.MaxDepth - 1);
               score = -AlphaBeta(in newPosition, searchDepth, -(alpha + 1), -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
            }
            
            if (score > alpha && score < beta)
            {
               var searchDepth = Math.Min(newDepth - 1 + extension, SearchConstants.MaxDepth - 1);
               score = -AlphaBeta(in newPosition, searchDepth, -beta, -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
            }
         }

         if (searchInfo.ShouldStop) return 0;

         if (score > bestScore)
         {
            bestScore = score;
            bestMove = move;

            if (score > alpha)
            {
               alpha = score;
               UpdatePV(ply, move);

               if (ply == 0)
               {
                  searchInfo.BestMove = move;
               }

               if (score >= beta)
               {
                  if (!move.IsCapture)
                  {
                     moveOrdering.UpdateKillers(move, ply);
                     moveOrdering.UpdateHistory(move, newDepth);
                  }
                  
                  tt.Store(position.Hash, bestMove, TranspositionTable.ScoreToTT((short)bestScore, ply),
                     (byte)newDepth, BoundType.Lower);

                  return bestScore;
               }
            }
            else if (ply == 0)
            {
               // At root, always update best move and PV even if score doesn't improve alpha
               // This ensures we have a move to play even in losing positions
               searchInfo.BestMove = move;
               UpdatePV(ply, move);
            }
         }
      }

      if (!searchedAnyMove)
      {
         return inCheck
            ? -SearchConstants.Checkmate + ply
            : SearchConstants.DrawScore;
      }

      var bound = bestScore <= originalAlpha ? BoundType.Upper :
         bestScore >= beta ? BoundType.Lower : BoundType.Exact;

      tt.Store(position.Hash, bestMove, TranspositionTable.ScoreToTT((short)bestScore, ply),
         (byte)newDepth, bound);

      return bestScore;
   }

   /// <summary>
   ///    Quiescence search to avoid horizon effect.
   ///    Only searches captures and checks.
   /// </summary>
   private int Quiescence(in Position position, int alpha, int beta, int ply)
   {
      searchInfo.Nodes++;
      
      // Check for stop signal
      if ((searchInfo.Nodes & 63) == 0)
      {
         if (searchInfo.ShouldStop) return 0;
         if (searchInfo.CancellationToken.IsCancellationRequested)
         {
            searchInfo.ShouldStop = true;
            return 0;
         }
      }

      var standPat = Evaluator.Evaluate(in position);

      if (standPat >= beta)
         return beta;

      if (standPat > alpha)
         alpha = standPat;

      var moveListSpan = moveBuffer.AsSpan(ply * 256, 256);
      var moveList = new MoveList(moveListSpan);
      MoveGenerator.GenerateCaptures(in position, ref moveList);

      var scoredMovesSpan = scoredMoveBuffer.AsSpan(ply * 256, moveList.Count);
      moveOrdering.ScoreMoves(moveList.Moves, scoredMovesSpan, moveList.Count, Move.Null, ply, in position);
      MoveOrdering.SortMoves(scoredMovesSpan, moveList.Count);

      for (var i = 0; i < moveList.Count; i++)
      {
         var move = scoredMovesSpan[i].Move;
         
         // SEE pruning in quiescence: skip bad captures
         if (!StaticExchangeEvaluation.SeeGreaterOrEqual(in position, move, 0))
            continue;

         var newPosition = position;
         newPosition.MakeMove(move);

         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;

         var score = -Quiescence(in newPosition, -beta, -alpha, ply + 1);

         if (score >= beta)
            return beta;

         if (score > alpha)
            alpha = score;
      }

      return alpha;
   }
   
   /// <summary>
   ///    Special alpha-beta search for singular extension detection.
   ///    Searches all moves except the excluded move to see if any reach the given beta.
   /// </summary>
   private int AlphaBetaSingular(in Position position, int depth, int alpha, int beta, int ply, Move excludedMove)
   {
      searchInfo.Nodes++;
      
      if (depth <= 0)
         return Quiescence(in position, alpha, beta, ply);
         
      var moveListSpan = moveBuffer.AsSpan(ply * 256, 256);
      var moveList = new MoveList(moveListSpan);
      MoveGenerator.GenerateMoves(in position, ref moveList);
      
      var bestScore = -SearchConstants.Infinity;
      
      for (var i = 0; i < moveList.Count; i++)
      {
         var move = moveList.Moves[i];
         
         // Skip the excluded move
         if (move.Equals(excludedMove))
            continue;
            
         var newPosition = position;
         newPosition.MakeMove(move);
         
         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;
            
         var score = -AlphaBeta(in newPosition, depth - 1, -beta, -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
         
         if (searchInfo.ShouldStop) return 0;
         
         if (score > bestScore)
         {
            bestScore = score;
            
            if (score >= beta)
               return beta;
               
            if (score > alpha)
               alpha = score;
         }
      }
      
      return bestScore;
   }

   /// <summary>
   ///    Checks if the position is a draw.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static bool IsDraw(in Position position)
   {
      if (position.HalfmoveClock >= 100)
         return true;

      if (Evaluator.IsInsufficientMaterial(in position))
         return true;

      return false;
   }

   /// <summary>
   ///    Checks if the side to move has non-pawn material.
   ///    Used to avoid null move in endgames where zugzwang is more likely.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static bool HasNonPawnMaterial(in Position position)
   {
      if (position.SideToMove == Color.White)
      {
         return (position.WhiteKnights | position.WhiteBishops | position.WhiteRooks | position.WhiteQueens) != 0;
      }
      else
      {
         return (position.BlackKnights | position.BlackBishops | position.BlackRooks | position.BlackQueens) != 0;
      }
   }

   /// <summary>
   ///    Prints search information during iterative deepening.
   /// </summary>
   private void PrintSearchInfo()
   {
      lock (uciStringBuilder) // Lock if StringBuilder is shared and SearchEngine can be multi-threaded
      {
         uciStringBuilder.Clear();
         var elapsed = searchInfo.Timer.ElapsedMilliseconds;
         var nodes = searchInfo.Nodes > 0 ? searchInfo.Nodes : 1; // Ensure at least 1 node
         var nps = searchInfo.GetNps();
         var hashfull = tt.GetHashFull();

         uciStringBuilder.Append("info depth ").Append(searchInfo.CurrentDepth);

         uciStringBuilder.Append(" score ");
         if (Math.Abs(searchInfo.BestScore) >= SearchConstants.CheckmateThreshold)
         {
            var mateIn = (SearchConstants.Checkmate - Math.Abs(searchInfo.BestScore) + 1) / 2;
            uciStringBuilder.Append("mate ").Append(searchInfo.BestScore > 0 ? mateIn : -mateIn);
         }
         else
         {
            uciStringBuilder.Append("cp ").Append(searchInfo.BestScore);
         }

         uciStringBuilder.Append(" nodes ").Append(nodes);
         uciStringBuilder.Append(" nps ").Append(nps);
         uciStringBuilder.Append(" time ").Append(elapsed);
         uciStringBuilder.Append(" hashfull ").Append(hashfull);
         uciStringBuilder.Append(" pv ");
         BuildPvString(uciStringBuilder); // Append PV to existing StringBuilder

         Console.WriteLine(uciStringBuilder.ToString());
         Console.Out.Flush(); // Ensure Fritz sees the output immediately
      }
   }
   
   /// <summary>
   ///    Builds the principal variation string from the PV table into a StringBuilder.
   /// </summary>
   private void BuildPvString(StringBuilder sb)
   {
      if (searchInfo.PvLength[0] == 0 || searchInfo.PvTable[0][0].IsNull)
      {
         if (!searchInfo.BestMove.IsNull)
         {
            sb.Append(searchInfo.BestMove.ToAlgebraic());
         }
         return;
      }
         
      for (int i = 0; i < searchInfo.PvLength[0]; i++)
      {
         if (i > 0)
         {
            sb.Append(' ');
         }
         sb.Append(searchInfo.PvTable[0][i].ToAlgebraic());
      }
   }
   
   /// <summary>
   ///    Checks if a move is a passed pawn push to the 7th rank.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static bool IsPassedPawnPush7th(in Position position, Move move)
   {
      // Check if it's a pawn move
      if (move.Piece.Type() != PieceType.Pawn || move.IsCapture)
         return false;
         
      var to = move.To;
      var toRank = to.Rank();
      
      // Check if pushing to 7th rank (rank 6 for white, rank 1 for black)
      if (position.SideToMove == Color.White && toRank != 6)
         return false;
      if (position.SideToMove == Color.Black && toRank != 1)
         return false;
         
      // Check if the pawn is passed
      var file = to.File();
      var enemyPawns = position.SideToMove == Color.White ? position.BlackPawns : position.WhitePawns;
      
      // Create mask for enemy pawns that could stop this pawn
      ulong passMask = 0;
      if (position.SideToMove == Color.White)
      {
         // Check files and ranks ahead
         for (int r = toRank + 1; r <= 7; r++)
         {
            for (int f = Math.Max(0, file - 1); f <= Math.Min(7, file + 1); f++)
            {
               passMask |= 1UL << (r * 8 + f);
            }
         }
      }
      else
      {
         // Check files and ranks ahead (for black, moving down)
         for (int r = toRank - 1; r >= 0; r--)
         {
            for (int f = Math.Max(0, file - 1); f <= Math.Min(7, file + 1); f++)
            {
               passMask |= 1UL << (r * 8 + f);
            }
         }
      }
      
      // If no enemy pawns can stop this pawn, it's passed
      return (passMask & enemyPawns) == 0;
   }
   
   /// <summary>
   ///    Updates the PV when a new best move is found.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private void UpdatePV(int ply, Move move)
   {
      // Copy the move to the PV
      searchInfo.PvTable[ply][0] = move;
      
      // Copy the rest of the PV from ply+1
      int nextPlyLength = searchInfo.PvLength[ply + 1];
      for (int i = 0; i < nextPlyLength; i++)
      {
         searchInfo.PvTable[ply][i + 1] = searchInfo.PvTable[ply + 1][i];
      }
      
      // Update PV length
      searchInfo.PvLength[ply] = nextPlyLength + 1;
   }
   
   /// <summary>
   ///    Extracts the ponder move from the principal variation.
   /// </summary>
   private void ExtractPonderMove()
   {
      searchInfo.PonderMove = Move.Null;
      
      // If we have at least 2 moves in the PV, the second move is the ponder move
      if (searchInfo.PvLength[0] >= 2 && !searchInfo.PvTable[0][1].IsNull)
      {
         searchInfo.PonderMove = searchInfo.PvTable[0][1];
      }
   }
}
