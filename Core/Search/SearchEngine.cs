namespace Meridian.Core.Search;

using System.Runtime.CompilerServices;

using Evaluation;

using MoveGeneration;

/// <summary>
///    The main search engine using alpha-beta pruning with quiescence search.
/// </summary>
public class SearchEngine
{
   private readonly Move[] moveBuffer = new Move[SearchConstants.MaxPly * 256];
   private readonly ScoredMove[] scoredMoveBuffer = new ScoredMove[SearchConstants.MaxPly * 256];
   private readonly SearchInfo searchInfo = new();
   private readonly TranspositionTable tt;
   private readonly MoveOrdering moveOrdering = new();
   private Position rootPosition; // Store the root position for PV extraction
   
   private static readonly int[,] LMRTable = new int[64, 64];
   private static readonly int[] FutilityMargins = new int[SearchConstants.FutilityMaxDepth + 1];

   static SearchEngine()
   {
      for (int depth = 1; depth < 64; depth++)
      {
         for (int moves = 1; moves < 64; moves++)
         {
            LMRTable[depth, moves] = (int)(0.75 + Math.Log(depth) * Math.Log(moves) / 2.25);
         }
      }
      
      for (int depth = 0; depth <= SearchConstants.FutilityMaxDepth; depth++)
      {
         FutilityMargins[depth] = SearchConstants.FutilityMarginBase * depth;
      }
   }

   public SearchEngine(int ttSizeMB = 128) => tt = new TranspositionTable(ttSizeMB);

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
   ///    Searches for the best move in the given position.
   /// </summary>
   public Move Search(Position position, int maxDepth, int maxTime = int.MaxValue, CancellationToken cancellationToken = default)
   {
      rootPosition = position; // Store for PV extraction
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

      for (var depth = 1; depth <= searchInfo.MaxDepth; depth++)
      {
         if (searchInfo.ShouldStop || cancellationToken.IsCancellationRequested) 
         {
            searchInfo.ShouldStop = true;
            break;
         }
         
         searchInfo.CurrentDepth = depth;
         
         if (depth >= 4 && searchInfo.BestScore > -SearchConstants.CheckmateThreshold && 
             searchInfo.BestScore < SearchConstants.CheckmateThreshold)
         {
            alpha = searchInfo.BestScore - aspirationDelta;
            beta = searchInfo.BestScore + aspirationDelta;
         }
         
         var score = AlphaBeta(position, depth, alpha, beta, 0);
         
         if (!searchInfo.ShouldStop)
         {
            if (score <= alpha || score >= beta)
            {
               aspirationDelta = Math.Min(aspirationDelta * 2, 500);
               alpha = -SearchConstants.Infinity;
               beta = SearchConstants.Infinity;
               score = AlphaBeta(position, depth, alpha, beta, 0);
            }
            
            if (!searchInfo.ShouldStop && searchInfo.Nodes > depth * 100) // Only output if we did real work
            {
               searchInfo.BestScore = score;
               PrintSearchInfo();
               aspirationDelta = 50;
            }
         }
      }

      searchInfo.Timer.Stop();
      
      // Output final info if we completed at least one depth
      if (searchInfo.CurrentDepth > 0 && searchInfo.BestMove != Move.Null && !searchInfo.ShouldStop)
      {
         PrintSearchInfo();
      }
      
      return searchInfo.BestMove;
   }

   /// <summary>
   ///    Alpha-beta search with fail-hard cutoffs.
   /// </summary>
   private int AlphaBeta(in Position position, int depth, int alpha, int beta, int ply, bool allowNull = true)
   {
      searchInfo.Nodes++;

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

         if (ttEntry.Depth >= depth)
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

      if (depth <= 0)
         return Quiescence(in position, alpha, beta, ply);

      if (allowNull && !inCheck && depth >= 3 && ply > 0 && HasNonPawnMaterial(in position))
      {
         var eval = Evaluator.Evaluate(in position);
         
         if (eval >= beta)
         {
            const int R = 3;
            var nullDepth = depth - R - 1;
            
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
         }
      }

      var canUseFutility = !inCheck && 
                           depth <= SearchConstants.FutilityMaxDepth && 
                           Math.Abs(alpha) < SearchConstants.CheckmateThreshold &&
                           Math.Abs(beta) < SearchConstants.CheckmateThreshold;
      
      var staticEval = 0;
      if (canUseFutility)
      {
         staticEval = Evaluator.Evaluate(in position);
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
      moveOrdering.ScoreMoves(moveList.Moves, scoredMovesSpan, moveList.Count, ttMove, ply);
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
            var score = -AlphaBeta(in newPosition, depth - 1, -beta, -alpha, ply + 1);

            if (searchInfo.ShouldStop) return 0;

            if (score > bestScore)
            {
               bestScore = score;
               bestMove = ttMove;

               if (score > alpha)
               {
                  alpha = score;

                  if (ply == 0)
                  {
                     searchInfo.BestMove = ttMove;
                     searchInfo.PrincipalVariation[0] = ttMove;
                  }

                  if (score >= beta)
                  {
                     tt.Store(position.Hash, bestMove, TranspositionTable.ScoreToTT((short)bestScore, ply),
                        (byte)depth, BoundType.Lower);

                     return bestScore;
                  }
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
         movesSearched++;
         
         if (canUseFutility && 
             !move.IsCapture && 
             !move.IsPromotion &&
             movesSearched > 1 &&
             staticEval + FutilityMargins[depth] <= alpha)
         {
            continue;
         }
         
         int score;
         var reduction = 0;
         
         if (depth >= SearchConstants.LMRMinDepth && 
             movesSearched > SearchConstants.LMRMinMoves &&
             !inCheck &&
             !move.IsCapture &&
             !move.IsPromotion)
         {
            reduction = LMRTable[Math.Min(depth, 63), Math.Min(movesSearched, 63)];
            reduction = Math.Min(depth - 2, Math.Max(reduction, 1));
         }
         
         if (movesSearched == 1)
         {
            score = -AlphaBeta(in newPosition, depth - 1, -beta, -alpha, ply + 1);
         }
         else
         {
            if (reduction > 0)
            {
               score = -AlphaBeta(in newPosition, depth - reduction - 1, -(alpha + 1), -alpha, ply + 1);
            }
            else
            {
               score = -AlphaBeta(in newPosition, depth - 1, -(alpha + 1), -alpha, ply + 1);
            }
            
            if (score > alpha && score < beta)
            {
               score = -AlphaBeta(in newPosition, depth - 1, -beta, -alpha, ply + 1);
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

               if (ply == 0)
               {
                  searchInfo.BestMove = move;
                  searchInfo.PrincipalVariation[0] = move;
               }

               if (score >= beta)
               {
                  if (!move.IsCapture)
                  {
                     moveOrdering.UpdateKillers(move, ply);
                     moveOrdering.UpdateHistory(move, depth);
                  }
                  
                  tt.Store(position.Hash, bestMove, TranspositionTable.ScoreToTT((short)bestScore, ply),
                     (byte)depth, BoundType.Lower);

                  return bestScore;
               }
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
         (byte)depth, bound);

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
      moveOrdering.ScoreMoves(moveList.Moves, scoredMovesSpan, moveList.Count, Move.Null, ply);
      MoveOrdering.SortMoves(scoredMovesSpan, moveList.Count);

      for (var i = 0; i < moveList.Count; i++)
      {
         var move = scoredMovesSpan[i].Move;

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
      var elapsed = searchInfo.Timer.ElapsedMilliseconds;
      var nps = searchInfo.GetNps();
      var hashfull = tt.GetHashFull();

      // Build PV string
      var pvString = BuildPvString();

      Console.WriteLine($"info depth {searchInfo.CurrentDepth} " +
                        $"score cp {searchInfo.BestScore} " +
                        $"nodes {searchInfo.Nodes} " +
                        $"nps {nps} " +
                        $"time {elapsed} " +
                        $"hashfull {hashfull} " +
                        $"pv {pvString}");
      Console.Out.Flush(); // Ensure Fritz sees the output immediately
   }
   
   /// <summary>
   ///    Builds the principal variation string from the transposition table.
   /// </summary>
   private string BuildPvString()
   {
      if (searchInfo.BestMove.IsNull)
         return "";
         
      var pvMoves = new List<Move>();
      var position = rootPosition;
      var seen = new HashSet<ulong>();
      
      // First move is always the best move from the root
      pvMoves.Add(searchInfo.BestMove);
      position.MakeMove(searchInfo.BestMove);
      seen.Add(position.Hash);
      
      // Allocate move buffer once outside the loop
      Span<Move> buffer = stackalloc Move[256];
      
      // Extract PV from transposition table
      for (int i = 1; i < 20; i++) // Limit PV length to 20 moves
      {
         if (tt.Probe(position.Hash, out var entry) && entry.BestMove != Move.Null)
         {
            var move = entry.BestMove;
            
            // Verify move is legal
            var moveList = new MoveList(buffer);
            MoveGenerator.GenerateMoves(in position, ref moveList);
            
            bool isLegal = false;
            for (int j = 0; j < moveList.Count; j++)
            {
               if (moveList.Moves[j].Equals(move))
               {
                  isLegal = true;
                  break;
               }
            }
            
            if (!isLegal)
               break;
               
            pvMoves.Add(move);
            position.MakeMove(move);
            
            // Avoid cycles in PV
            if (!seen.Add(position.Hash))
               break;
         }
         else
         {
            break;
         }
      }
      
      return string.Join(" ", pvMoves.Select(m => m.ToAlgebraic()));
   }
}
