namespace Meridian.Core.Search;

using System.Diagnostics;
using System.Threading;

/// <summary>
/// Thread-safe shared search information for coordinating multiple search threads.
/// </summary>
public class SharedSearchInfo
{
   private readonly object syncLock = new();
   private volatile bool shouldStop;
   private long totalNodes;
   private Move bestMove = Move.Null;
   private int bestScore = -SearchConstants.Infinity;
   private int bestDepth;
   private Move ponderMove = Move.Null;
   private Move[] principalVariation = new Move[SearchConstants.MaxDepth];
   private int pvLength;
   private long lastReportedNodes;
   
   // Timing
   private readonly Stopwatch timer = new();
   private int maxTime = int.MaxValue;
   
   /// <summary>
   /// Gets or sets whether the search should stop.
   /// </summary>
   public bool ShouldStop
   {
      get => shouldStop;
      set => shouldStop = value;
   }
   
   /// <summary>
   /// Initializes the shared info for a new search.
   /// </summary>
   public void Initialize(int maxTimeMs)
   {
      lock (syncLock)
      {
         shouldStop = false;
         totalNodes = 1; // Start with 1 to ensure GUI sees non-zero
         bestMove = Move.Null;
         bestScore = -SearchConstants.Infinity;
         bestDepth = 0;
         ponderMove = Move.Null;
         pvLength = 0;
         Array.Clear(principalVariation, 0, principalVariation.Length);
         maxTime = maxTimeMs;
         timer.Restart();
         lastReportedNodes = 0;
      }
   }
   
   /// <summary>
   /// Updates the best move if the new one is better.
   /// </summary>
   public void UpdateBestMove(Move move, int score, int depth, int threadId, Move[] pv, int pvLen, Move ponder = default)
   {
      lock (syncLock)
      {
         // Update if: deeper search, or same depth but better score
         bool shouldUpdate = depth > bestDepth || 
                           (depth == bestDepth && score > bestScore);
                           
         if (shouldUpdate && !move.IsNull)
         {
            bestMove = move;
            bestScore = score;
            bestDepth = depth;
            
            if (!ponder.IsNull)
               ponderMove = ponder;
            
            // Copy PV
            pvLength = Math.Min(pvLen, SearchConstants.MaxDepth);
            if (pvLength > 0)
            {
               Array.Copy(pv, principalVariation, pvLength);
            }
         }
      }
   }
   
   /// <summary>
   /// Gets the current best move.
   /// </summary>
   public (Move move, int score, int depth) GetBestMove()
   {
      lock (syncLock)
      {
         return (bestMove, bestScore, bestDepth);
      }
   }
   
   /// <summary>
   /// Gets the current best move with ponder move.
   /// </summary>
   public (Move move, Move ponder, int score, int depth) GetBestMoveWithPonder()
   {
      lock (syncLock)
      {
         return (bestMove, ponderMove, bestScore, bestDepth);
      }
   }
   
   /// <summary>
   /// Adds nodes to the total count.
   /// </summary>
   public void AddNodes(long nodes)
   {
      Interlocked.Add(ref totalNodes, nodes);
   }
   
   /// <summary>
   /// Gets the total nodes searched.
   /// </summary>
   public long GetTotalNodes()
   {
      return Interlocked.Read(ref totalNodes);
   }
   
   /// <summary>
   /// Reports search progress (called by main thread).
   /// </summary>
   public void ReportProgress(int depth, int score)
   {
      long nodes = GetTotalNodes();
      long elapsed = timer.ElapsedMilliseconds;
      
      // Ensure monotonically increasing node count
      if (nodes <= lastReportedNodes)
      {
         nodes = lastReportedNodes + 1;
      }
      lastReportedNodes = nodes;
      
      long nps = elapsed > 0 ? nodes * 1000 / elapsed : nodes * 1000;
      
      // Build PV string
      string pvString = BuildPvString();
      
      // Format score - use mate notation for checkmate scores
      string scoreStr;
      if (Math.Abs(score) >= SearchConstants.CheckmateThreshold)
      {
         // Convert to mate in N moves
         int mateIn = (SearchConstants.Checkmate - Math.Abs(score) + 1) / 2;
         scoreStr = score > 0 ? $"mate {mateIn}" : $"mate -{mateIn}";
      }
      else
      {
         scoreStr = $"cp {score}";
      }
      
      Console.WriteLine($"info depth {depth} score {scoreStr} " +
                       $"nodes {nodes} nps {nps} time {elapsed} " +
                       $"pv {pvString}");
      Console.Out.Flush();
      
      // Check time limit after reporting
      if (maxTime != int.MaxValue && timer.ElapsedMilliseconds > maxTime)
      {
         shouldStop = true;
      }
   }
   
   /// <summary>
   /// Stops the timer and returns elapsed time.
   /// </summary>
   public long StopTimer()
   {
      timer.Stop();
      return timer.ElapsedMilliseconds;
   }
   
   /// <summary>
   /// Builds the PV string from the stored principal variation.
   /// </summary>
   public string BuildPvString()
   {
      lock (syncLock)
      {
         if (pvLength == 0 || principalVariation[0].IsNull)
         {
            // Fallback to just best move
            return bestMove.IsNull ? "" : bestMove.ToAlgebraic();
         }
         
         var pvMoves = new string[pvLength];
         for (int i = 0; i < pvLength; i++)
         {
            pvMoves[i] = principalVariation[i].ToAlgebraic();
         }
         
         return string.Join(" ", pvMoves);
      }
   }
}