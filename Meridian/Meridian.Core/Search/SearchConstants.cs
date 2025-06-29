#nullable enable

namespace Meridian.Core.Search;

public static class SearchConstants
{
    public const int MaxDepth = 128;
    public const int MaxPly = 256;
    public const int Infinity = 32000;
    public const int MateScore = 31000;
    public const int MateInMaxPly = MateScore - MaxPly;
}