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
    public SearchEngine SearchEngine { get; set; } = null!;

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
        4 => -1,   // Helper 4: depth - 1 (for faster shallow search)
        5 => 3,    // Helper 5: depth + 3
        _ => (ThreadId % 4) - 1  // Others: cycle through -1, 0, 1, 2
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
        4 => 50,    // Helper 4: slightly wider window
        5 => 150,   // Helper 5: much wider window
        _ => (ThreadId % 3) * 50  // Others: cycle through 0, 50, 100
    };

    /// <summary>
    /// Gets null move reduction adjustment for this thread
    /// </summary>
    public int NullMoveReductionAdjustment => ThreadId switch
    {
        0 => 0,     // Main thread: normal reduction
        1 => 0,     // Helper 1: normal reduction
        2 => 1,     // Helper 2: more aggressive null move
        3 => 0,     // Helper 3: normal reduction
        4 => -1,    // Helper 4: less aggressive null move
        _ => (ThreadId % 2) - 1  // Others: alternate between -1 and 0
    };

    /// <summary>
    /// Gets LMR reduction adjustment for this thread
    /// </summary>
    public int LmrReductionAdjustment => ThreadId switch
    {
        0 => 0,     // Main thread: normal LMR
        1 => 0,     // Helper 1: normal LMR
        2 => 1,     // Helper 2: more aggressive LMR
        3 => 0,     // Helper 3: normal LMR
        4 => -1,    // Helper 4: less aggressive LMR
        _ => (ThreadId % 2) - 1  // Others: alternate between -1 and 0
    };

    /// <summary>
    /// Gets history score multiplier for this thread
    /// </summary>
    public double HistoryScoreMultiplier => ThreadId switch
    {
        0 => 1.0,   // Main thread: normal history
        1 => 1.0,   // Helper 1: normal history
        2 => 0.8,   // Helper 2: less history influence
        3 => 1.2,   // Helper 3: more history influence
        _ => 1.0 + (ThreadId % 3 - 1) * 0.1  // Others: slight variations
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
