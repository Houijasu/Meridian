#nullable enable

using System.Collections.Concurrent;
using Meridian.Core.Board;

namespace Meridian.Core.Search;

public sealed class ThreadPool : IDisposable
{
    private readonly List<SearchThread> _threads;
    private readonly TranspositionTable _transpositionTable;
    private readonly ConcurrentBag<SearchResult> _results;
    private readonly object _bestMoveLock = new();
    private Position? _rootPosition;
    private SearchLimits? _limits;
    private volatile bool _stopSearch;
    private Move _bestMove = Move.None;
    private int _bestScore;
    private ThreadData? _bestThreadData;
    
    public int ThreadCount => _threads.Count;
    public Move BestMove => _bestMove;
    public int BestScore => _bestScore;
    public ThreadData? BestThreadData => _bestThreadData;
    
    public ThreadPool(TranspositionTable transpositionTable, int threadCount = 1)
    {
        _transpositionTable = transpositionTable;
        _threads = new List<SearchThread>(threadCount);
        _results = new ConcurrentBag<SearchResult>();
        
        for (var i = 0; i < threadCount; i++)
        {
            _threads.Add(new SearchThread(i, _transpositionTable, this));
        }
    }
    
    public void StartSearch(Position position, SearchLimits limits)
    {
        _rootPosition = position;
        _limits = limits;
        _stopSearch = false;
        _bestMove = Move.None;
        _bestScore = -SearchConstants.Infinity;
        _bestThreadData = null;
        
        while (_results.TryTake(out _)) { }
        
        _transpositionTable.NewSearch();
        
        // Start all threads
        for (var i = 0; i < _threads.Count; i++)
        {
            var thread = _threads[i];
            var depthOffset = i == 0 ? 0 : 1 + (i % 4); // Helper threads search different depths
            thread.StartSearch(position, limits, depthOffset);
        }
    }
    
    public void StopSearch()
    {
        _stopSearch = true;
        foreach (var thread in _threads)
        {
            thread.Stop();
        }
    }
    
    public void WaitForSearchComplete()
    {
        foreach (var thread in _threads)
        {
            thread.WaitForSearchComplete();
        }
    }
    
    public void UpdateBestMove(Move move, int score, ThreadData threadData)
    {
        lock (_bestMoveLock)
        {
            if (score > _bestScore || (_bestMove == Move.None && move != Move.None))
            {
                _bestMove = move;
                _bestScore = score;
                _bestThreadData = threadData;
            }
        }
    }
    
    public bool IsSearchStopped() => _stopSearch;
    
    public void Dispose()
    {
        StopSearch();
        foreach (var thread in _threads)
        {
            thread.Dispose();
        }
        _threads.Clear();
    }
    
    public void SetThreadCount(int count)
    {
        if (count == _threads.Count)
            return;
            
        // Stop and dispose existing threads
        StopSearch();
        foreach (var thread in _threads)
        {
            thread.Dispose();
        }
        _threads.Clear();
        
        // Create new threads
        for (var i = 0; i < count; i++)
        {
            _threads.Add(new SearchThread(i, _transpositionTable, this));
        }
    }
}

public sealed class SearchResult
{
    public Move BestMove { get; }
    public int Score { get; }
    public int Depth { get; }
    public ThreadData ThreadData { get; }
    
    public SearchResult(Move bestMove, int score, int depth, ThreadData threadData)
    {
        BestMove = bestMove;
        Score = score;
        Depth = depth;
        ThreadData = threadData;
    }
}