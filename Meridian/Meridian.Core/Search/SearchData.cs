#nullable enable
using System.Threading;
using Meridian.Core.Board;

namespace Meridian.Core.Search;

/// <summary>
/// Holds all data structures that are private to a single search thread.
/// This prevents race conditions when running the engine with multiple threads.
/// </summary>
public class SearchData
{
    private readonly Move[,] _pvTable = new Move[SearchConstants.MaxPly, SearchConstants.MaxPly];
    private readonly int[] _pvLength = new int[SearchConstants.MaxPly];
    private readonly Move[,] _killerMoves = new Move[SearchConstants.MaxPly, 2];
    private readonly Move[] _moveStack = new Move[SearchConstants.MaxPly];

    private long _nodeCount;
    public long NodeCount => Interlocked.Read(ref _nodeCount);

    public Move[,] GetPvTable() => _pvTable;
    public int[] GetPvLength() => _pvLength;
    public Move[,] GetKillerMoves() => _killerMoves;
    public Move[] GetMoveStack() => _moveStack;

    public void IncrementNodeCount()
    {
        Interlocked.Increment(ref _nodeCount);
    }

    /// <summary>
    /// Clears the thread-local data structures to prepare for a new search.
    /// </summary>
    public void Clear()
    {
        _nodeCount = 0;
        for (var i = 0; i < SearchConstants.MaxPly; i++)
        {
            _pvLength[i] = 0;
            _killerMoves[i, 0] = Move.None;
            _killerMoves[i, 1] = Move.None;
            _moveStack[i] = Move.None;
        }
    }
}
