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
   private int bestThreadId = -1;
   
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
         totalNodes = 0;
         bestMove = Move.Null;
         bestScore = -SearchConstants.Infinity;
         bestDepth = 0;
         bestThreadId = -1;
         maxTime = maxTimeMs;
         timer.Restart();
      }
   }
   
   /// <summary>
   /// Updates the best move if the new one is better.
   /// </summary>
   public void UpdateBestMove(Move move, int score, int depth, int threadId)
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
            bestThreadId = threadId;
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
      // Check time limit
      if (maxTime != int.MaxValue && timer.ElapsedMilliseconds > maxTime)
      {
         shouldStop = true;
         return;
      }
      
      long nodes = GetTotalNodes();
      long elapsed = timer.ElapsedMilliseconds;
      long nps = elapsed > 0 ? nodes * 1000 / elapsed : 0;
      
      // Get current best move for PV
      var (move, _, _) = GetBestMove();
      string pvString = move.IsNull ? "" : move.ToAlgebraic();
      
      Console.WriteLine($"info depth {depth} score cp {score} " +
                       $"nodes {nodes} nps {nps} time {elapsed} " +
                       $"pv {pvString}");
      Console.Out.Flush();
   }
   
   /// <summary>
   /// Stops the timer and returns elapsed time.
   /// </summary>
   public long StopTimer()
   {
      timer.Stop();
      return timer.ElapsedMilliseconds;
   }
}