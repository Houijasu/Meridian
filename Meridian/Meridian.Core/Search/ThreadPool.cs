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
    private Move _globalBestMove;
    private volatile int _globalBestScore;
    private Position _searchPosition = null!;
    private SearchLimits _searchLimits = null!;
    private readonly object _bestMoveLock = new();

    public Action? OnProgressUpdate { get; set; }

    public ThreadPool(int threadCount, int ttSizeMb)
    {
        if (threadCount < 1 || threadCount > 256)
            throw new ArgumentException("Thread count must be between 1 and 256", nameof(threadCount));

        _sharedTT = new TranspositionTable(ttSizeMb);
        _threads = new ThreadData[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            var searchData = new SearchData();
            _threads[i] = new ThreadData
            {
                ThreadId = i,
                SearchEngine = new SearchEngine(_sharedTT, searchData, _historyScores)
            };
        }
    }

    public Move StartSearch(Position position, SearchLimits limits)
    {
        _globalBestMove = Move.None;
        _globalBestScore = -SearchConstants.Infinity;
        _searchPosition = new Position(position);
        _searchLimits = limits;

        _sharedTT.NewSearch();
        Array.Clear(_historyScores, 0, _historyScores.Length);

        foreach (var thread in _threads)
        {
            thread.Reset();
        }

        var tasks = new Task<Move>[_threads.Length];
        for (int i = 0; i < _threads.Length; i++)
        {
            var threadData = _threads[i];
            tasks[i] = Task.Run(() => SearchThread(threadData));
        }

        Task.WhenAll(tasks).Wait();
        return _globalBestMove;
    }

    public void StopAll()
    {
        foreach (var thread in _threads)
        {
            thread.SearchEngine.Stop();
        }
    }

    public SearchInfo GetAggregatedInfo()
    {
        var mainThread = _threads[0];
        var info = mainThread.SearchEngine.SearchInfo;
        info.Nodes = _threads.Sum(t => t.Nodes);
        info.Nps = info.Time > 0 ? (info.Nodes * 1000) / info.Time : 0;
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

    private Move SearchThread(ThreadData threadData)
    {
        try
        {
            threadData.IsSearching = true;
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

            threadData.SearchEngine.OnSearchProgress = (info) =>
            {
                threadData.CompletedDepth = info.Depth;

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
                    if (info.Score > _globalBestScore)
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

            return threadData.SearchEngine.StartSearch(position, limits);
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