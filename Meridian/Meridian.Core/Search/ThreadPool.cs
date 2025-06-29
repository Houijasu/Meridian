#nullable enable

using System.Collections.Concurrent;
using Meridian.Core.Board;

namespace Meridian.Core.Search;

/// <summary>
/// Manages a pool of search threads for Lazy SMP parallel search
/// </summary>
public sealed class ThreadPool : IDisposable
{
    private readonly ThreadData[] _threads;
    private readonly TranspositionTable _sharedTT;
    private Move _globalBestMove;  // Not volatile - protected by _bestMoveLock
    private volatile int _globalBestScore;
    private Position _searchPosition = null!;
    private SearchLimits _searchLimits = null!;
    private readonly object _bestMoveLock = new();
    
    /// <summary>
    /// Callback invoked when search makes progress
    /// </summary>
    public Action? OnProgressUpdate { get; set; }
    
    public ThreadPool(int threadCount, int ttSizeMb)
    {
        if (threadCount < 1 || threadCount > 256)
            throw new ArgumentException("Thread count must be between 1 and 256", nameof(threadCount));
            
        // Console.WriteLine($"info string Creating ThreadPool with {threadCount} threads");
        _sharedTT = new TranspositionTable(ttSizeMb);
        _threads = new ThreadData[threadCount];
        
        // Create thread data for each thread
        for (int i = 0; i < threadCount; i++)
        {
            _threads[i] = new ThreadData
            {
                ThreadId = i,
                SearchEngine = new SearchEngine(_sharedTT)
            };
        }
    }
    
    /// <summary>
    /// Starts parallel search on all threads
    /// </summary>
    public void StartSearch(Position position, SearchLimits limits)
    {
        _globalBestMove = Move.None;
        _globalBestScore = -SearchConstants.Infinity;
        _searchPosition = new Position(position);
        _searchLimits = limits;
        
        // Console.WriteLine($"info string StartSearch called with {_threads.Length} threads");
        
        // Clear shared TT age for new search
        _sharedTT.NewSearch();
        
        // Reset all threads
        foreach (var thread in _threads)
        {
            thread.Reset();
        }
        
        // Start search on all threads
        for (int i = 0; i < _threads.Length; i++)
        {
            var threadData = _threads[i];
            threadData.IsSearching = true;
            
            // Console.WriteLine($"info string Starting thread {i}");
            
            // All threads run asynchronously
            var localThreadData = threadData; // Capture by value
            var localThreadIndex = i; // Capture by value
            var thread = new Thread(() => SearchThread(localThreadData))
            {
                IsBackground = true,
                Name = $"Search Thread {localThreadIndex}"
            };
            threadData.Thread = thread;
            thread.Start();
            // Console.WriteLine($"info string Thread {i} started");
        }
    }
    
    /// <summary>
    /// Waits for search to complete and returns best move
    /// </summary>
    public Move WaitForBestMove()
    {
        // Wait for all threads to finish (including thread 0)
        for (int i = 0; i < _threads.Length; i++)
        {
            _threads[i].Thread?.Join();
        }
        
        // Console.WriteLine($"info string WaitForBestMove returning {_globalBestMove.ToUci()}");
        return _globalBestMove;
    }
    
    /// <summary>
    /// Stops all search threads
    /// </summary>
    public void StopAll()
    {
        // Signal all search engines to stop
        foreach (var thread in _threads)
        {
            thread.SearchEngine.Stop();
        }
        
        // Wait for threads to stop
        for (int i = 1; i < _threads.Length; i++)
        {
            _threads[i].Thread?.Join(100);
        }
    }
    
    /// <summary>
    /// Gets aggregated search information
    /// </summary>
    public SearchInfo GetAggregatedInfo()
    {
        var info = new SearchInfo();
        
        long totalNodes = 0;
        int maxDepth = 0;
        int maxSelDepth = 0;
        int maxTime = 0;
        
        foreach (var thread in _threads)
        {
            // Include all threads, even if just started
            totalNodes += thread.Nodes;
            if (thread.CompletedDepth > 0)
            {
                // For depth reporting, use the main thread's depth if available
                if (thread.ThreadId == 0)
                {
                    maxDepth = thread.CompletedDepth;
                }
                else if (maxDepth == 0)
                {
                    maxDepth = Math.Max(maxDepth, thread.CompletedDepth);
                }
            }
            maxSelDepth = Math.Max(maxSelDepth, thread.SelDepth);
            if (thread.SearchEngine.SearchInfo.Time > 0)
            {
                maxTime = Math.Max(maxTime, thread.SearchEngine.SearchInfo.Time);
            }
        }
        
        info.Nodes = totalNodes;
        info.Depth = maxDepth;
        info.SelectiveDepth = maxSelDepth > 0 ? maxSelDepth : 1;
        info.Score = _globalBestScore;
        info.Time = maxTime;
        
        // Copy PV from main thread if available - without modifying original
        if (_threads.Length > 0)
        {
            var mainPv = _threads[0].SearchEngine.SearchInfo.PrincipalVariation;
            if (!mainPv.IsEmpty)
            {
                // Create a copy of the PV without dequeuing
                foreach (var move in mainPv)
                {
                    info.PrincipalVariation.Enqueue(move);
                }
            }
        }
        
        return info;
    }
    
    /// <summary>
    /// Gets the maximum selective depth across all threads
    /// </summary>
    public int GetMaxSelectiveDepth()
    {
        int maxSelDepth = 0;
        foreach (var thread in _threads)
        {
            maxSelDepth = Math.Max(maxSelDepth, thread.SelDepth);
        }
        return maxSelDepth > 0 ? maxSelDepth : 1;
    }
    
    /// <summary>
    /// Gets the transposition table usage in permille
    /// </summary>
    public int GetHashfull()
    {
        return _sharedTT.Usage();
    }
    
    /// <summary>
    /// Resizes the shared transposition table
    /// </summary>
    public void ResizeTranspositionTable(int sizeMb)
    {
        // Note: This is not thread-safe during search
        // Should only be called when not searching
        foreach (var thread in _threads)
        {
            thread.SearchEngine.SetTranspositionTable(new TranspositionTable(sizeMb));
        }
    }
    
    private void SearchThread(ThreadData threadData)
    {
        try
        {
            // Console.WriteLine($"info string Thread {threadData.ThreadId} starting search");
            var position = new Position(_searchPosition);
            var limits = new SearchLimits
            {
                Depth = _searchLimits.Depth,
                MoveTime = _searchLimits.MoveTime,
                Infinite = _searchLimits.Infinite,
                WhiteTime = _searchLimits.WhiteTime,
                BlackTime = _searchLimits.BlackTime,
                WhiteIncrement = _searchLimits.WhiteIncrement,
                BlackIncrement = _searchLimits.BlackIncrement,
                MovesToGo = _searchLimits.MovesToGo
            };
            
            // Adjust depth for helper threads
            if (threadData.ThreadId > 0 && limits.Depth > 0)
            {
                var adjustment = threadData.DepthAdjustment;
                // Ensure we don't exceed safe limits - leave 20 ply margin for extensions
                limits.Depth = Math.Min(limits.Depth + adjustment, SearchConstants.MaxDepth - 20);
            }
            
            // Configure search engine callbacks
            threadData.SearchEngine.OnSearchProgress = (info) =>
            {
                // Update thread's current state
                var depthChanged = info.Depth > threadData.CompletedDepth;
                threadData.CompletedDepth = info.Depth;
                threadData.BestScore = info.Score;
                
                // Signal progress update if depth changed
                if (depthChanged)
                {
                    OnProgressUpdate?.Invoke();
                }
                
                // Update global best move if this thread found better
                // For main thread (id 0), always update if we have a move
                if (threadData.ThreadId == 0 && !info.PrincipalVariation.IsEmpty)
                {
                    lock (_bestMoveLock)
                    {
                        if (info.PrincipalVariation.TryPeek(out var bestMove))
                        {
                            _globalBestMove = bestMove;
                            _globalBestScore = info.Score;
                        }
                    }
                }
                else if (info.Score > _globalBestScore && !info.PrincipalVariation.IsEmpty)
                {
                    lock (_bestMoveLock)
                    {
                        if (info.Score > _globalBestScore)
                        {
                            _globalBestScore = info.Score;
                            if (info.PrincipalVariation.TryPeek(out var bestMove))
                            {
                                _globalBestMove = bestMove;
                            }
                        }
                    }
                }
            };
            
            // Set thread-specific parameters
            if (threadData.ThreadId > 0)
            {
                threadData.SearchEngine.SetHelperThreadParameters(
                    threadData.ThreadId,
                    threadData.AspirationWindowAdjustment
                );
            }
            
            // Start search
            var bestMove = threadData.SearchEngine.StartSearch(position, limits);
            threadData.BestMove = bestMove;
            
            // Update global best move from main thread's result
            if (threadData.ThreadId == 0 && bestMove != Move.None)
            {
                lock (_bestMoveLock)
                {
                    _globalBestMove = bestMove;
                }
            }
            // Console.WriteLine($"info string Thread {threadData.ThreadId} finished search, best move: {bestMove.ToUci()}");
        }
        finally
        {
            threadData.IsSearching = false;
        }
    }
    
    public void Dispose()
    {
        StopAll();
    }
}