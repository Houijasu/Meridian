namespace Meridian.Core.Search;

using System.Diagnostics;

/// <summary>
///    Contains information about the current search.
/// </summary>
public class SearchInfo
{
    /// <summary>
    ///    Stopwatch for timing the search.
    /// </summary>
    public Stopwatch Timer { get; } = new();

    /// <summary>
    ///    Number of nodes searched.
    /// </summary>
    public long Nodes { get; set; }

    /// <summary>
    ///    Maximum depth to search.
    /// </summary>
    public int MaxDepth { get; set; } = SearchConstants.MaxDepth;

    /// <summary>
    ///    Maximum time to search in milliseconds.
    /// </summary>
    public int MaxTime { get; set; } = int.MaxValue;

    /// <summary>
    ///    Whether the search should be stopped.
    /// </summary>
    public volatile bool ShouldStop;
    
    /// <summary>
    ///    Cancellation token for external cancellation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    ///    Current search depth.
    /// </summary>
    public int CurrentDepth { get; set; }

    /// <summary>
    ///    Best move found so far.
    /// </summary>
    public Move BestMove { get; set; }

    /// <summary>
    ///    Score of the best move.
    /// </summary>
    public int BestScore { get; set; }

    /// <summary>
    ///    Principal variation (best line).
    /// </summary>
    public Move[] PrincipalVariation { get; } = new Move[SearchConstants.MaxDepth];

    /// <summary>
    ///    Length of the principal variation.
    /// </summary>
    public int PvLength { get; set; }

    /// <summary>
    ///    Checks if we should stop the search due to time limit.
    /// </summary>
    public void CheckTime()
   {
      if (Timer.ElapsedMilliseconds > MaxTime)
         ShouldStop = true;
   }

    /// <summary>
    ///    Gets nodes per second.
    /// </summary>
    public long GetNps()
   {
      var elapsed = Timer.ElapsedMilliseconds;

      return elapsed > 0
         ? Nodes * 1000 / elapsed
         : 0;
   }
}
