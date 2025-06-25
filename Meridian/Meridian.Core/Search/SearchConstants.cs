#nullable enable

namespace Meridian.Core.Search;

public static class SearchConstants
{
    public const int MaxDepth = 100;
    public const int Infinity = 32000;
    public const int MateScore = 31000;
    public const int MateInMaxPly = MateScore - MaxDepth;
}