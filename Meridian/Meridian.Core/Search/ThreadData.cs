#nullable enable

using Meridian.Core.Board;

namespace Meridian.Core.Search;

/// <summary>
/// Encapsulates per-thread state for parallel search
/// </summary>
public sealed class ThreadData
{
    /// <summary>
    /// Unique thread identifier (0 = main thread)
    /// </summary>
    public int ThreadId { get; init; }
    
    /// <summary>
    /// Thread's own search engine instance
    /// </summary>
    public SearchEngine SearchEngine { get; init; } = null!;
    
    /// <summary>
    /// The actual thread object
    /// </summary>
    public Thread? Thread { get; set; }
    
    /// <summary>
    /// Indicates if this thread is currently searching
    /// </summary>
    public bool IsSearching { get; set; }
    
    /// <summary>
    /// The best move found by this thread
    /// </summary>
    public Move BestMove { get; set; }
    
    /// <summary>
    /// The score of the best move
    /// </summary>
    public int BestScore { get; set; }
    
    /// <summary>
    /// Maximum depth reached by this thread
    /// </summary>
    public int CompletedDepth { get; set; }
    
    /// <summary>
    /// Nodes searched by this thread
    /// </summary>
    public long Nodes => SearchEngine.SearchInfo.Nodes;
    
    /// <summary>
    /// Selective depth reached
    /// </summary>
    public int SelDepth => SearchEngine.SelectiveDepth;
    
    /// <summary>
    /// Gets depth adjustment for this thread
    /// </summary>
    public int DepthAdjustment => ThreadId switch
    {
        0 => 0,    // Main thread: normal depth
        1 => 1,    // Helper 1: depth + 1
        2 => 0,    // Helper 2: normal depth, wider window
        3 => 2,    // Helper 3: depth + 2
        _ => ThreadId % 3  // Others: cycle through 0, 1, 2
    };
    
    /// <summary>
    /// Gets aspiration window adjustment for this thread
    /// </summary>
    public int AspirationWindowAdjustment => ThreadId switch
    {
        0 => 0,     // Main thread: normal window
        1 => 0,     // Helper 1: normal window
        2 => 100,   // Helper 2: wider window
        3 => 0,     // Helper 3: normal window
        _ => (ThreadId % 2) * 50  // Others: alternate between normal and wider
    };
    
    /// <summary>
    /// Resets thread state for new search
    /// </summary>
    public void Reset()
    {
        BestMove = Move.None;
        BestScore = -SearchConstants.Infinity;
        CompletedDepth = 0;
        SearchEngine.SearchInfo.Clear();
    }
}