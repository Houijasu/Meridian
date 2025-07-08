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
    private readonly int[,,] _historyScores = new int[2, 64, 64];
    private readonly Move[,] _counterMoves = new Move[64, 64];
    private Move _globalBestMove;
    private volatile int _globalBestScore;
    private Position _searchPosition = null!;
    private SearchLimits _searchLimits = null!;
    private readonly object _bestMoveLock = new();
    private volatile bool _stopSearch;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _threadsReadySignal;
    private volatile int _completedThreads;
    private volatile int _maxCompletedDepth;
    private volatile bool _targetDepthReached;

    public Action? OnProgressUpdate { get; set; }

    public ThreadPool(int threadCount, int ttSizeMb)
    {
        if (threadCount < 1 || threadCount > 256)
            throw new ArgumentException("Thread count must be between 1 and 256", nameof(threadCount));

        _sharedTT = new TranspositionTable(ttSizeMb);
        _threads = new ThreadData[threadCount];
        _threadsReadySignal = new SemaphoreSlim(0, threadCount);

        for (int i = 0; i < threadCount; i++)
        {
            var searchData = new SearchData();
            _threads[i] = new ThreadData
            {
                ThreadId = i,
                SearchEngine = null!
            };
            // Set the search engine with thread data reference after creation
            _threads[i].SearchEngine = new SearchEngine(_sharedTT, searchData, _historyScores, _counterMoves, _threads[i]);
        }
    }

    public Move StartSearch(Position position, SearchLimits limits)
    {
        _globalBestMove = Move.None;
        _globalBestScore = -SearchConstants.Infinity;
        _searchPosition = new Position(position);
        _searchLimits = limits;
        _stopSearch = false;
        _completedThreads = 0;
        _maxCompletedDepth = 0;
        _targetDepthReached = false;

        _sharedTT.NewSearch();
        Array.Clear(_historyScores, 0, _historyScores.Length);
        Array.Clear(_counterMoves, 0, _counterMoves.Length);

        foreach (var thread in _threads)
        {
            thread.Reset();
        }

        var tasks = new Task<Move>[_threads.Length];
        for (int i = 0; i < _threads.Length; i++)
        {
            var threadData = _threads[i];
            tasks[i] = Task.Run(() => SearchThread(threadData, _cancellationTokenSource.Token));
        }

        try
        {
            Task.WhenAll(tasks).Wait(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping search
        }

        return _globalBestMove;
    }

    public void StopAll()
    {
        _stopSearch = true;
        _cancellationTokenSource.Cancel();

        foreach (var thread in _threads)
        {
            thread.SearchEngine.Stop();
        }
    }

    public SearchInfo GetAggregatedInfo()
    {
        var mainThread = _threads[0];
        var info = mainThread.SearchEngine.SearchInfo;

        // Aggregate nodes from all threads
        info.Nodes = _threads.Sum(t => t.Nodes);
        info.Nps = info.Time > 0 ? (info.Nodes * 1000) / info.Time : 0;

        // Use the main thread's depth for reporting, capped at original limit for fixed depth searches
        if (_searchLimits.Depth > 0 && !_searchLimits.Infinite)
        {
            info.Depth = Math.Min(info.Depth, _searchLimits.Depth);
        }

        return info;
    }

    public int GetMaxSelectiveDepth()
    {
        return _threads.Max(t => t.SelDepth);
    }

    public int GetHashfull()
    {
        return _sharedTT.Usage();
    }

    public void ResizeTranspositionTable(int sizeMb)
    {
        var newTT = new TranspositionTable(sizeMb);
        foreach (var thread in _threads)
        {
            thread.SearchEngine.SetTranspositionTable(newTT);
        }
    }

    private Move SearchThread(ThreadData threadData, CancellationToken cancellationToken)
    {
        try
        {
            threadData.IsSearching = true;
            var position = new Position(_searchPosition);

            // Apply thread-specific modifications for diversity
            var adjustedDepth = _searchLimits.Depth;

            // Only apply depth adjustments for infinite searches or time-based searches
            if (_searchLimits.Infinite || _searchLimits.Depth <= 0)
            {
                adjustedDepth = Math.Max(1, _searchLimits.Depth + threadData.DepthAdjustment);
            }

            var limits = new SearchLimits
            {
                Depth = adjustedDepth,
                MoveTime = _searchLimits.MoveTime,
                Infinite = _searchLimits.Infinite,
                WhiteTime = _searchLimits.WhiteTime,
                BlackTime = _searchLimits.BlackTime,
                WhiteIncrement = _searchLimits.WhiteIncrement,
                BlackIncrement = _searchLimits.BlackIncrement,
                MovesToGo = _searchLimits.MovesToGo
            };

            threadData.SearchEngine.OnSearchProgress = (info) =>
            {
                if (cancellationToken.IsCancellationRequested || _stopSearch)
                    return;

                threadData.CompletedDepth = info.Depth;

                // Update maximum completed depth across all threads
                lock (_bestMoveLock)
                {
                    // For fixed depth searches, don't report beyond the requested depth
                    if (_searchLimits.Depth > 0 && !_searchLimits.Infinite)
                    {
                        var cappedDepth = Math.Min(info.Depth, _searchLimits.Depth);
                        _maxCompletedDepth = Math.Max(_maxCompletedDepth, cappedDepth);

                        // Stop all threads when main thread reaches target depth
                        if (threadData.ThreadId == 0 && info.Depth >= _searchLimits.Depth)
                        {
                            _targetDepthReached = true;
                            _stopSearch = true;
                        }
                    }
                    else
                    {
                        _maxCompletedDepth = Math.Max(_maxCompletedDepth, info.Depth);
                    }
                }

                // Stop if target depth reached
                if (_targetDepthReached)
                {
                    threadData.SearchEngine.Stop();
                    return;
                }

                // Main thread always updates global best move
                if (threadData.ThreadId == 0)
                {
                    lock (_bestMoveLock)
                    {
                        if (!info.PrincipalVariation.IsEmpty && info.PrincipalVariation.TryPeek(out var bestMove))
                        {
                            _globalBestMove = bestMove;
                        }
                        _globalBestScore = info.Score;
                    }
                }
                else
                {
                    // Helper threads only update if they find a better move
                    if (info.Score > _globalBestScore + 10) // Small margin to reduce lock contention
                    {
                        lock (_bestMoveLock)
                        {
                            if (info.Score > _globalBestScore)
                            {
                                _globalBestScore = info.Score;
                                if (!info.PrincipalVariation.IsEmpty && info.PrincipalVariation.TryPeek(out var bestMove))
                                {
                                    _globalBestMove = bestMove;
                                }
                            }
                        }
                    }
                }

                OnProgressUpdate?.Invoke();
            };

            // Start the search with cancellation support
            var searchTask = Task.Run(() => threadData.SearchEngine.StartSearch(position, limits), cancellationToken);

            try
            {
                return searchTask.Result;
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                return _globalBestMove;
            }
        }
        finally
        {
            threadData.IsSearching = false;
            Interlocked.Increment(ref _completedThreads);
        }
    }

    public void Dispose()
    {
        StopAll();
        _cancellationTokenSource.Dispose();
        _threadsReadySignal.Dispose();
    }
}
