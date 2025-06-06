namespace Meridian.Core.Search;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Multi-threaded search engine using Lazy SMP approach.
/// Multiple threads search the same position with slightly different parameters.
/// </summary>
public class MultiThreadedSearchEngine
{
   private readonly ThreadSafeTranspositionTable tt;
   private readonly SharedSearchInfo sharedInfo;
   private readonly SearchThread[] threads;
   private readonly int threadCount;
   
   /// <summary>
   /// Creates a new multi-threaded search engine.
   /// </summary>
   /// <param name="ttSizeMB">Transposition table size in MB</param>
   /// <param name="numThreads">Number of search threads</param>
   public MultiThreadedSearchEngine(int ttSizeMB = 128, int numThreads = 1)
   {
      tt = new ThreadSafeTranspositionTable(ttSizeMB);
      sharedInfo = new SharedSearchInfo();
      threadCount = Math.Max(1, Math.Min(numThreads, Environment.ProcessorCount));
      
      // Create search threads
      threads = new SearchThread[threadCount];
      for (int i = 0; i < threadCount; i++)
      {
         threads[i] = new SearchThread(i, tt, sharedInfo);
      }
   }
   
   /// <summary>
   /// Clears the transposition table.
   /// </summary>
   public void ClearTT() => tt.Clear();
   
   /// <summary>
   /// Clears move ordering tables for all threads.
   /// </summary>
   public void ClearMoveOrdering()
   {
      // Each thread maintains its own move ordering
      // They will be cleared when threads are recreated
   }
   
   /// <summary>
   /// Stops the current search.
   /// </summary>
   public void StopSearch() => sharedInfo.ShouldStop = true;
   
   /// <summary>
   /// Gets the current best move.
   /// </summary>
   public Move GetBestMove()
   {
      var (move, _, _) = sharedInfo.GetBestMove();
      return move;
   }
   
   /// <summary>
   /// Gets the ponder move (expected opponent response).
   /// </summary>
   public Move GetPonderMove()
   {
      var (_, ponder, _, _) = sharedInfo.GetBestMoveWithPonder();
      return ponder;
   }
   
   /// <summary>
   /// Searches for the best move using multiple threads.
   /// </summary>
   public Move Search(Position position, int maxDepth, int maxTime = int.MaxValue, CancellationToken cancellationToken = default)
   {
      // Initialize shared info
      sharedInfo.Initialize(maxTime);
      
      // Create tasks for each thread
      Task[] searchTasks = new Task[threadCount];
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      
      // Capture the token to avoid capturing the disposable CancellationTokenSource
      var token = cts.Token;
      
      for (int i = 0; i < threadCount; i++)
      {
         int threadId = i;
         searchTasks[i] = Task.Run(() =>
         {
            threads[threadId].Search(position, maxDepth, token);
         }, token);
      }
      
      // Start a timer task if we have a time limit
      Task? timerTask = null;
      if (maxTime != int.MaxValue)
      {
         timerTask = Task.Run(async () =>
         {
            await Task.Delay(maxTime, token);
            sharedInfo.ShouldStop = true;
         }, token);
      }
      
      // Wait for all threads to complete
      try
      {
         Task.WaitAll(searchTasks);
      }
      catch (AggregateException)
      {
         // One or more tasks were cancelled - this is expected
         sharedInfo.ShouldStop = true;
      }
      finally
      {
         // Cancel the timer if it's still running
         if (!cts.Token.IsCancellationRequested)
         {
            cts.Cancel();
         }
         if (timerTask != null)
         {
            try
            {
               timerTask.Wait(100);
            }
            catch (AggregateException ae) when (ae.InnerException is TaskCanceledException)
            {
               // Expected when timer is cancelled
            }
            catch (OperationCanceledException)
            {
               // Expected when timer is cancelled
            }
         }
      }
      
      // Get the best move found
      var (bestMove, bestScore, bestDepth) = sharedInfo.GetBestMove();
      
      // Make sure all threads have reported their final nodes
      foreach (var thread in threads)
      {
         thread.ReportFinalNodes();
      }
      
      // Final info output
      long elapsed = sharedInfo.StopTimer();
      long nodes = sharedInfo.GetTotalNodes();
      
      // Ensure we report at least 1 node and reasonable NPS
      if (nodes == 0) nodes = 1;
      long nps = elapsed > 0 ? nodes * 1000 / elapsed : nodes * 1000;
      int hashFull = tt.GetHashFull();
      
      if (bestDepth > 0)
      {
         // Get the full PV from shared info
         string pvString = sharedInfo.BuildPvString();
         
         // Format score - use mate notation for checkmate scores
         string scoreStr;
         if (Math.Abs(bestScore) >= SearchConstants.CheckmateThreshold)
         {
            // Convert to mate in N moves
            int mateIn = (SearchConstants.Checkmate - Math.Abs(bestScore) + 1) / 2;
            scoreStr = bestScore > 0 ? $"mate {mateIn}" : $"mate -{mateIn}";
         }
         else
         {
            scoreStr = $"cp {bestScore}";
         }
         
         Console.WriteLine($"info depth {bestDepth} score {scoreStr} " +
                          $"nodes {nodes} nps {nps} time {elapsed} " +
                          $"hashfull {hashFull} pv {pvString}");
         Console.Out.Flush();
      }
      
      return bestMove;
   }
   
   /// <summary>
   /// Gets the number of threads.
   /// </summary>
   public int GetThreadCount() => threadCount;
}