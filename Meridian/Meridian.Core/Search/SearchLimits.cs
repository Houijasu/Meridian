#nullable enable

namespace Meridian.Core.Search;

public sealed class SearchLimits
{
    public int WhiteTime { get; set; }
    public int BlackTime { get; set; }
    public int WhiteIncrement { get; set; }
    public int BlackIncrement { get; set; }
    public int MovesToGo { get; set; }
    public int Depth { get; set; }
    public int MoveTime { get; set; }
    public bool Infinite { get; set; }
    
    public void Clear()
    {
        WhiteTime = 0;
        BlackTime = 0;
        WhiteIncrement = 0;
        BlackIncrement = 0;
        MovesToGo = 0;
        Depth = 0;
        MoveTime = 0;
        Infinite = false;
    }
}