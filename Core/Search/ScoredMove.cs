namespace Meridian.Core.Search;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// A move with an associated score for move ordering.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ScoredMove
{
    public Move Move;
    public int Score;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ScoredMove(Move move, int score)
    {
        Move = move;
        Score = score;
    }
}