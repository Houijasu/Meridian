namespace Meridian.Core.Search;

using System.Runtime.CompilerServices;
using System.Threading;
using Evaluation;
using MoveGeneration;

/// <summary>
/// Represents a worker thread for parallel search.
/// Each thread has its own search state and buffers.
/// </summary>
public class SearchThread
{
   // Thread-local buffers to avoid allocations and contention
   private readonly Move[] moveBuffer;
   private readonly ScoredMove[] scoredMoveBuffer;
   private readonly MoveOrdering moveOrdering;
   
   // Shared resources (thread-safe)
   private readonly ThreadSafeTranspositionTable tt;
   private readonly SharedSearchInfo sharedInfo;
   
   // Thread-specific state
   private readonly SearchInfo localInfo;
   public int ThreadId { get; }
   private Position rootPosition;
   
   // LMR and futility tables (read-only, can be shared)
   private static readonly int[,] LMRTable = new int[64, 64];
   private static readonly int[] FutilityMargins = new int[SearchConstants.FutilityMaxDepth + 1];
   
   static SearchThread()
   {
      // Initialize LMR table
      for (int depth = 1; depth < 64; depth++)
      {
         for (int moves = 1; moves < 64; moves++)
         {
            LMRTable[depth, moves] = (int)(0.75 + Math.Log(depth) * Math.Log(moves) / 2.25);
         }
      }
      
      // Initialize futility margins
      for (int depth = 0; depth <= SearchConstants.FutilityMaxDepth; depth++)
      {
         FutilityMargins[depth] = SearchConstants.FutilityMarginBase * depth;
      }
   }
   
   public SearchThread(int threadId, ThreadSafeTranspositionTable transpositionTable, SharedSearchInfo sharedSearchInfo)
   {
      ThreadId = threadId;
      tt = transpositionTable;
      sharedInfo = sharedSearchInfo;
      
      // Allocate thread-local resources
      moveBuffer = new Move[SearchConstants.MaxPly * 256];
      scoredMoveBuffer = new ScoredMove[SearchConstants.MaxPly * 256];
      moveOrdering = new MoveOrdering();
      localInfo = new SearchInfo();
   }
   
   /// <summary>
   /// Main search entry point for this thread.
   /// </summary>
   public void Search(Position position, int depth, CancellationToken cancellationToken)
   {
      rootPosition = position;
      localInfo.Nodes = 0;
      localInfo.MaxDepth = Math.Min(depth, SearchConstants.MaxDepth);
      localInfo.CancellationToken = cancellationToken;
      localInfo.Timer.Restart();
      localInfo.BestMove = Move.Null;
      localInfo.BestScore = -SearchConstants.Infinity;
      
      // For Lazy SMP, each thread searches with slightly different parameters
      int depthOffset = ThreadId % 2; // Alternating threads search different depths
      int targetDepth = Math.Min(localInfo.MaxDepth + depthOffset, SearchConstants.MaxDepth);
      
      // Aspiration windows - vary by thread to increase diversity
      int aspirationDelta = 50 + ThreadId * 10;
      int alpha = -SearchConstants.Infinity;
      int beta = SearchConstants.Infinity;
      
      Move bestMove = Move.Null;
      int bestScore = -SearchConstants.Infinity;
      
      for (int d = 1; d <= targetDepth; d++)
      {
         if (sharedInfo.ShouldStop || cancellationToken.IsCancellationRequested)
            break;
            
         // Use aspiration windows starting from depth 4
         if (d >= 4 && bestScore > -SearchConstants.CheckmateThreshold && 
             bestScore < SearchConstants.CheckmateThreshold)
         {
            alpha = bestScore - aspirationDelta;
            beta = bestScore + aspirationDelta;
         }
         
         int score = AlphaBeta(rootPosition, d, alpha, beta, 0, true, Square.None);
         
         if (sharedInfo.ShouldStop || cancellationToken.IsCancellationRequested)
            break;
            
         // Handle aspiration window failures
         if (score <= alpha || score >= beta)
         {
            aspirationDelta = Math.Min(aspirationDelta * 2, 500);
            alpha = -SearchConstants.Infinity;
            beta = SearchConstants.Infinity;
            score = AlphaBeta(rootPosition, d, alpha, beta, 0, true, Square.None);
         }
         
         if (!sharedInfo.ShouldStop && !cancellationToken.IsCancellationRequested)
         {
            bestScore = score;
            bestMove = localInfo.BestMove;
            
            // Extract ponder move if we have a best move
            Move ponderMove = Move.Null;
            if (!bestMove.IsNull)
            {
               ponderMove = ExtractPonderMove(rootPosition, bestMove);
            }
            
            // Update shared best move if this is better
            sharedInfo.UpdateBestMove(bestMove, bestScore, d, ThreadId, ponderMove);
            
            // Report progress from thread 0 only
            if (ThreadId == 0)
            {
               sharedInfo.ReportProgress(d, score);
            }
         }
      }
      
      // Don't add nodes here - will be done in ReportFinalNodes
   }
   
   /// <summary>
   /// Reports final node count to shared info.
   /// </summary>
   public void ReportFinalNodes()
   {
      // Report any remaining nodes not yet reported
      if (localInfo.Nodes > 0)
      {
         sharedInfo.AddNodes(localInfo.Nodes);
         localInfo.Nodes = 0;
      }
   }
   
   /// <summary>
   /// Alpha-beta search implementation (similar to single-threaded version).
   /// </summary>
   private int AlphaBeta(in Position position, int depth, int alpha, int beta, int ply, bool allowNull = true, Square lastCaptureSquare = Square.None)
   {
      localInfo.Nodes++;
      
      // Check for stop conditions and update shared node count periodically
      if ((localInfo.Nodes & 2047) == 0)
      {
         // Report nodes to shared info
         sharedInfo.AddNodes(2048);
         localInfo.Nodes -= 2048; // Reset local counter to avoid overflow
         
         if (sharedInfo.ShouldStop || localInfo.CancellationToken.IsCancellationRequested)
            return 0;
      }
      
      // Draw detection
      if (ply > 0 && IsDraw(in position))
         return SearchConstants.DrawScore;
         
      // Mate distance pruning
      int originalAlpha = alpha;
      alpha = Math.Max(alpha, -SearchConstants.Checkmate + ply);
      beta = Math.Min(beta, SearchConstants.Checkmate - ply);
      if (alpha >= beta) return alpha;
      
      bool inCheck = AttackDetection.IsKingInCheck(in position, position.SideToMove);
      
      // Transposition table probe
      Move ttMove = Move.Null;
      if (tt.Probe(position.Hash, out var ttEntry))
      {
         ttMove = ttEntry.BestMove;
         
         if (ttEntry.Depth >= depth)
         {
            int ttScore = ThreadSafeTranspositionTable.ScoreFromTT(ttEntry.Score, ply);
            
            switch (ttEntry.Bound)
            {
               case BoundType.Exact:
                  if (ply == 0 && ttMove != Move.Null)
                  {
                     localInfo.BestMove = ttMove;
                  }
                  return ttScore;
                  
               case BoundType.Lower:
                  alpha = Math.Max(alpha, ttScore);
                  break;
                  
               case BoundType.Upper:
                  beta = Math.Min(beta, ttScore);
                  break;
            }
            
            if (alpha >= beta)
            {
               if (ply == 0 && ttMove != Move.Null)
               {
                  localInfo.BestMove = ttMove;
               }
               return ttScore;
            }
         }
      }
      
      // Extensions
      int extensions = 0;
      if (inCheck)
      {
         extensions += SearchConstants.CheckExtension;
      }
      
      int newDepth = depth + extensions;
      
      // Drop into quiescence search
      if (newDepth <= 0)
         return Quiescence(in position, alpha, beta, ply);
         
      // Null move pruning
      if (allowNull && !inCheck && newDepth >= 3 && ply > 0 && HasNonPawnMaterial(in position))
      {
         int eval = Evaluator.Evaluate(in position);
         
         if (eval >= beta)
         {
            const int R = 3;
            int nullDepth = newDepth - R - 1;
            
            Position nullPosition = position;
            nullPosition.SideToMove = nullPosition.SideToMove.Flip();
            nullPosition.EnPassantSquare = Square.None;
            nullPosition.Hash ^= Zobrist.GetSideKey(Color.Black);
            if (position.EnPassantSquare != Square.None)
               nullPosition.Hash ^= Zobrist.GetEnPassantKey(position.EnPassantSquare);
               
            int nullScore = -AlphaBeta(in nullPosition, nullDepth, -beta, -beta + 1, ply + 1, false, Square.None);
            
            if (sharedInfo.ShouldStop) return 0;
            
            if (nullScore >= beta)
            {
               if (nullScore >= SearchConstants.Checkmate - SearchConstants.MaxPly)
                  nullScore = beta;
               return nullScore;
            }
         }
      }
      
      // Static evaluation for pruning decisions
      int staticEval = 0;
      bool improving = false;
      if (!inCheck)
      {
         staticEval = Evaluator.Evaluate(in position);
         improving = staticEval > alpha - 50;
      }
      
      // Move generation
      var moveListSpan = moveBuffer.AsSpan(ply * 256, 256);
      var moveList = new MoveList(moveListSpan);
      MoveGenerator.GenerateMoves(in position, ref moveList);
      
      if (moveList.Count == 0)
      {
         return inCheck ? -SearchConstants.Checkmate + ply : SearchConstants.DrawScore;
      }
      
      // Score and sort moves
      var scoredMovesSpan = scoredMoveBuffer.AsSpan(ply * 256, moveList.Count);
      moveOrdering.ScoreMoves(moveList.Moves, scoredMovesSpan, moveList.Count, ttMove, ply, in position);
      MoveOrdering.SortMoves(scoredMovesSpan, moveList.Count);
      
      
      Move bestMove = Move.Null;
      int bestScore = -SearchConstants.Infinity;
      bool searchedAnyMove = false;
      int movesSearched = 0;
      
      // Search moves
      for (int i = 0; i < moveList.Count; i++)
      {
         Move move = scoredMovesSpan[i].Move;
         
         Position newPosition = position;
         newPosition.MakeMove(move);
         
         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;
            
         searchedAnyMove = true;
         movesSearched++;
         
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
             !move.IsCapture &&
             !move.IsPromotion &&
             bestScore > -SearchConstants.CheckmateThreshold)
         {
            continue;
         }
         
         int score;
         int reduction = 0;
         int extension = 0;
         
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
         
         // Late move reductions
         if (newDepth >= SearchConstants.LMRMinDepth && 
             movesSearched > SearchConstants.LMRMinMoves &&
             !inCheck && !move.IsCapture && !move.IsPromotion)
         {
            reduction = LMRTable[Math.Min(newDepth, 63), Math.Min(movesSearched, 63)];
            reduction = Math.Min(newDepth - 2, Math.Max(reduction, 1));
         }
         
         // Principal variation search
         if (movesSearched == 1)
         {
            score = -AlphaBeta(in newPosition, newDepth - 1 + extension, -beta, -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
         }
         else
         {
            // Search with null window
            score = -AlphaBeta(in newPosition, newDepth - reduction - 1 + extension, -(alpha + 1), -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
            
            // Re-search if it fails high
            if (score > alpha && score < beta)
            {
               score = -AlphaBeta(in newPosition, newDepth - 1 + extension, -beta, -alpha, ply + 1, true, move.IsCapture ? move.To : Square.None);
            }
         }
         
         if (sharedInfo.ShouldStop) return 0;
         
         if (score > bestScore)
         {
            bestScore = score;
            bestMove = move;
            
            if (score > alpha)
            {
               alpha = score;
               
               if (ply == 0)
               {
                  localInfo.BestMove = move;
               }
               
               if (score >= beta)
               {
                  // Update move ordering heuristics
                  if (!move.IsCapture)
                  {
                     moveOrdering.UpdateKillers(move, ply);
                     moveOrdering.UpdateHistory(move, newDepth);
                  }
                  
                  // Store in transposition table
                  tt.Store(position.Hash, bestMove, 
                          ThreadSafeTranspositionTable.ScoreToTT((short)bestScore, ply),
                          (byte)newDepth, BoundType.Lower);
                          
                  return bestScore;
               }
            }
         }
      }
      
      if (!searchedAnyMove)
      {
         return inCheck ? -SearchConstants.Checkmate + ply : SearchConstants.DrawScore;
      }
      
      // Determine bound type and store in TT
      BoundType bound = bestScore <= originalAlpha ? BoundType.Upper :
                       bestScore >= beta ? BoundType.Lower : BoundType.Exact;
                       
      tt.Store(position.Hash, bestMove,
              ThreadSafeTranspositionTable.ScoreToTT((short)bestScore, ply),
              (byte)newDepth, bound);
              
      return bestScore;
   }
   
   /// <summary>
   /// Quiescence search to handle captures and checks.
   /// </summary>
   private int Quiescence(in Position position, int alpha, int beta, int ply)
   {
      localInfo.Nodes++;
      
      // Check for stop conditions and update shared node count periodically
      if ((localInfo.Nodes & 2047) == 0)
      {
         // Report nodes to shared info
         sharedInfo.AddNodes(2048);
         localInfo.Nodes -= 2048; // Reset local counter to avoid overflow
         
         if (sharedInfo.ShouldStop || localInfo.CancellationToken.IsCancellationRequested)
            return 0;
      }
      
      // Stand pat evaluation
      int standPat = Evaluator.Evaluate(in position);
      
      if (standPat >= beta)
         return beta;
         
      if (standPat > alpha)
         alpha = standPat;
         
      // Generate only captures
      var moveListSpan = moveBuffer.AsSpan(ply * 256, 256);
      var moveList = new MoveList(moveListSpan);
      MoveGenerator.GenerateCaptures(in position, ref moveList);
      
      // Score and sort captures
      var scoredMovesSpan = scoredMoveBuffer.AsSpan(ply * 256, moveList.Count);
      moveOrdering.ScoreMoves(moveList.Moves, scoredMovesSpan, moveList.Count, Move.Null, ply, in position);
      MoveOrdering.SortMoves(scoredMovesSpan, moveList.Count);
      
      for (int i = 0; i < moveList.Count; i++)
      {
         Move move = scoredMovesSpan[i].Move;
         
         // SEE pruning in quiescence: skip bad captures
         if (!StaticExchangeEvaluation.SeeGreaterOrEqual(in position, move, 0))
            continue;
         
         Position newPosition = position;
         newPosition.MakeMove(move);
         
         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;
            
         int score = -Quiescence(in newPosition, -beta, -alpha, ply + 1);
         
         if (score >= beta)
            return beta;
            
         if (score > alpha)
            alpha = score;
      }
      
      return alpha;
   }
   
   private static bool IsDraw(in Position position)
   {
      if (position.HalfmoveClock >= 100)
         return true;
         
      if (Evaluator.IsInsufficientMaterial(in position))
         return true;
         
      return false;
   }
   
   private static bool HasNonPawnMaterial(in Position position)
   {
      if (position.SideToMove == Color.White)
      {
         return (position.WhiteKnights | position.WhiteBishops | 
                position.WhiteRooks | position.WhiteQueens) != 0;
      }
      else
      {
         return (position.BlackKnights | position.BlackBishops | 
                position.BlackRooks | position.BlackQueens) != 0;
      }
   }
   
   /// <summary>
   /// Checks if a move is a passed pawn push to the 7th rank.
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
   /// Extracts the ponder move from the transposition table.
   /// </summary>
   private Move ExtractPonderMove(Position position, Move bestMove)
   {
      if (bestMove.IsNull)
         return Move.Null;
         
      // Make the best move
      var ponderPosition = position;
      ponderPosition.MakeMove(bestMove);
      
      // Look for the expected response in the TT
      if (tt.Probe(ponderPosition.Hash, out var entry) && entry.BestMove != Move.Null)
      {
         // Verify the move is legal
         Span<Move> buffer = stackalloc Move[256];
         var moveList = new MoveList(buffer);
         MoveGenerator.GenerateMoves(in ponderPosition, ref moveList);
         
         for (int i = 0; i < moveList.Count; i++)
         {
            if (moveList.Moves[i].Equals(entry.BestMove))
            {
               return entry.BestMove;
            }
         }
      }
      
      return Move.Null;
   }
}