namespace Meridian.Core.Search;

/// <summary>
///    Constants used throughout the search engine.
/// </summary>
public static class SearchConstants
{
    /// <summary>
    ///    Maximum search depth supported.
    /// </summary>
    public const int MaxDepth = 128;

    /// <summary>
    ///    Maximum ply (half-moves) from root position.
    /// </summary>
    public const int MaxPly = 128;

    /// <summary>
    ///    Value representing positive infinity in search.
    /// </summary>
    public const int Infinity = 30000;

    /// <summary>
    ///    Value representing checkmate.
    /// </summary>
    public const int Checkmate = 20000;

    /// <summary>
    ///    Minimum value that represents a checkmate score.
    /// </summary>
    public const int CheckmateThreshold = Checkmate - MaxPly;

    /// <summary>
    ///    Value representing a draw.
    /// </summary>
    public const int DrawScore = 0;

    /// <summary>
    ///    Invalid/unknown score.
    /// </summary>
    public const int NoScore = -Infinity - 1;
    
    /// <summary>
    ///    Minimum depth for Late Move Reductions.
    /// </summary>
    public const int LMRMinDepth = 3;
    
    /// <summary>
    ///    Minimum moves searched before applying LMR.
    /// </summary>
    public const int LMRMinMoves = 4;
    
    /// <summary>
    ///    Base futility margin per ply.
    /// </summary>
    public const int FutilityMarginBase = 200;
    
    /// <summary>
    ///    Maximum depth for futility pruning.
    /// </summary>
    public const int FutilityMaxDepth = 6;
    
    /// <summary>
    ///    Extension amount for check positions.
    /// </summary>
    public const int CheckExtension = 1;
    
    /// <summary>
    ///    Minimum depth for singular extension.
    /// </summary>
    public const int SingularExtensionMinDepth = 6;
    
    /// <summary>
    ///    Depth reduction for singular extension search.
    /// </summary>
    public const int SingularExtensionDepthReduction = 3;
    
    /// <summary>
    ///    Margin for singular extension detection (in centipawns).
    /// </summary>
    public const int SingularExtensionMargin = 50;
    
    /// <summary>
    ///    Extension amount for recaptures.
    /// </summary>
    public const int RecaptureExtension = 1;
    
    /// <summary>
    ///    Extension amount for passed pawns pushing to 7th rank.
    /// </summary>
    public const int PassedPawnExtension = 1;
    
    /// <summary>
    ///    Maximum depth for Late Move Pruning.
    /// </summary>
    public const int LMPMaxDepth = 3;
    
    /// <summary>
    ///    Base number of moves to search before LMP kicks in.
    /// </summary>
    public const int LMPBaseMovesSearched = 3;
    
    /// <summary>
    ///    Late Move Pruning move count table indexed by depth.
    ///    Defines how many moves to search at each depth before pruning.
    /// </summary>
    public static readonly int[] LMPMoveCount = new int[]
    {
        0,   // depth 0 - not used
        3,   // depth 1 - search 3 moves
        6,   // depth 2 - search 6 moves  
        12,  // depth 3 - search 12 moves
    };
    
    /// <summary>
    ///    Late Move Pruning improving factor.
    ///    Additional moves to search when position is improving.
    /// </summary>
    public const int LMPImprovingBonus = 2;
}
