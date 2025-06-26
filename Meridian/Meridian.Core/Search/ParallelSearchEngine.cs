#nullable enable

using Meridian.Core.Board;

namespace Meridian.Core.Search;

public sealed class ParallelSearchEngine : IDisposable
{
    private TranspositionTable _transpositionTable;
    private readonly ThreadPool _threadPool;
    private int _threadCount;
    
    public SearchInfo SearchInfo => _threadPool.BestThreadData?.Info ?? new SearchInfo();
    
    public ParallelSearchEngine(int ttSizeMb = 128, int threadCount = 1)
    {
        _transpositionTable = new TranspositionTable(ttSizeMb);
        _threadPool = new ThreadPool(_transpositionTable, threadCount);
        _threadCount = threadCount;
    }
    
    public Move StartSearch(Position position, SearchLimits limits)
    {
        if (position == null || limits == null) return Move.None;
        
        _threadPool.StartSearch(position, limits);
        _threadPool.WaitForSearchComplete();
        
        return _threadPool.BestMove;
    }
    
    public void Stop()
    {
        _threadPool.StopSearch();
    }
    
    public int GetHashfull() => _transpositionTable.Usage();
    
    public void ResizeTranspositionTable(int sizeMb)
    {
        if (sizeMb != _transpositionTable.SizeMb)
        {
            _transpositionTable = new TranspositionTable(sizeMb);
            
            // Recreate thread pool with new TT
            var oldThreadCount = _threadPool.ThreadCount;
            _threadPool.Dispose();
            _threadPool.SetThreadCount(0);
            _threadPool.SetThreadCount(oldThreadCount);
        }
    }
    
    public void SetThreadCount(int count)
    {
        if (count < 1) count = 1;
        if (count > 512) count = 512;
        
        _threadCount = count;
        _threadPool.SetThreadCount(count);
    }
    
    public int ThreadCount => _threadCount;
    
    public void Dispose()
    {
        _threadPool.Dispose();
    }
}