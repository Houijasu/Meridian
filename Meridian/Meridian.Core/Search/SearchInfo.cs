#nullable enable

using Meridian.Core.Board;

namespace Meridian.Core.Search;

public sealed class SearchInfo
{
    public int Depth { get; set; }
    public int Score { get; set; }
    public long Nodes { get; set; }
    public int Time { get; set; }
    public List<Move> PrincipalVariation { get; } = new();
    
    public void Clear()
    {
        Depth = 0;
        Score = 0;
        Nodes = 0;
        Time = 0;
        PrincipalVariation.Clear();
    }
    
    public int NodesPerSecond => Time > 0 ? (int)(Nodes * 1000 / Time) : 0;
}