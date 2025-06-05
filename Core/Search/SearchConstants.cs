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
}
