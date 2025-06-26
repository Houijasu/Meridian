#nullable enable

using Meridian.Core.Board;

namespace Meridian.Core.Search;

public sealed class SearchInfo
{
    public int Depth { get; set; }
    public int Score { get; set; }
    public long Nodes { get; set; }
    public int Time { get; set; }
    public List<Move> PrincipalVariation { get; } = new(); // CA1002: Internal usage, performance critical
    public long PvsReSearches { get; set; }
    public long PvsHits { get; set; }
    public int AspirationHits { get; set; }
    public int AspirationMisses { get; set; }
    
    public void Clear()
    {
        Depth = 0;
        Score = 0;
        Nodes = 0;
        Time = 0;
        PrincipalVariation.Clear();
        PvsReSearches = 0;
        PvsHits = 0;
        AspirationHits = 0;
        AspirationMisses = 0;
    }
    
    public int NodesPerSecond => Time > 0 ? (int)(Nodes * 1000 / Time) : 0;
    public double PvsHitRate => PvsHits + PvsReSearches > 0 ? (double)PvsHits / (PvsHits + PvsReSearches) * 100 : 0;
    public double AspirationHitRate => AspirationHits + AspirationMisses > 0 ? 
        (double)AspirationHits / (AspirationHits + AspirationMisses) * 100 : 0;
}