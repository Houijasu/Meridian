#nullable enable

using Meridian.Core.Board;

namespace Meridian.Core.Search;

public sealed class ThreadData
{
    public int ThreadId { get; }
    public int RootDepth { get; set; }
    internal Move[] PvTable { get; }
    internal int[] PvLength { get; }
    internal Move[,] KillerMoves { get; }
    internal int[,,] HistoryScores { get; }
    public SearchInfo Info { get; }
    internal volatile bool ShouldStop;
    
    public ThreadData(int threadId)
    {
        ThreadId = threadId;
        PvTable = new Move[SearchConstants.MaxDepth * SearchConstants.MaxDepth];
        PvLength = new int[SearchConstants.MaxDepth];
        KillerMoves = new Move[SearchConstants.MaxDepth, 2];
        HistoryScores = new int[2, 64, 64]; // [color, from, to]
        Info = new SearchInfo();
    }
    
    public void Clear()
    {
        Array.Clear(PvTable);
        Array.Clear(PvLength);
        Array.Clear(KillerMoves);
        Array.Clear(HistoryScores);
        Info.Clear();
        ShouldStop = false;
        RootDepth = 0;
    }
    
    public void UpdatePrincipalVariation(Move move, int ply)
    {
        // Get the starting index in the 1D array for the current ply's PV
        var pvIndex = ply * SearchConstants.MaxDepth;
        // Get the starting index for the child ply's PV
        var nextPvIndex = (ply + 1) * SearchConstants.MaxDepth;
        
        // The first move of the PV for this node is the best move we just found
        PvTable[pvIndex] = move;
        
        // The rest of the PV is the PV from the child node
        var childPvLength = PvLength[ply + 1];
        if (childPvLength > 0)
        {
            // Copy the child's PV into the current PV, right after the first move
            Array.Copy(PvTable, nextPvIndex, PvTable, pvIndex + 1, childPvLength);
        }
        
        // The length of the current PV is 1 (for our move) + the length of the child's PV
        PvLength[ply] = childPvLength + 1;
        
        // Update the user-facing SearchInfo only at the root of the search
        if (ply == 0)
        {
            Info.PrincipalVariation.Clear();
            for (var i = 0; i < PvLength[0]; i++)
            {
                Info.PrincipalVariation.Add(PvTable[i]);
            }
        }
    }
    
    public void UpdateKillerMoves(Move move, int ply)
    {
        if (ply < SearchConstants.MaxDepth && !move.IsCapture)
        {
            if (KillerMoves[ply, 0] != move)
            {
                KillerMoves[ply, 1] = KillerMoves[ply, 0];
                KillerMoves[ply, 0] = move;
            }
        }
    }
    
    public bool IsKillerMove(Move move, int ply)
    {
        return ply < SearchConstants.MaxDepth && 
               (move == KillerMoves[ply, 0] || move == KillerMoves[ply, 1]);
    }
    
    
    public void UpdateHistoryScore(Move move, int bonus, Color color)
    {
        if (move.IsCapture || move.IsPromotion)
            return;
            
        var colorIndex = color == Color.White ? 0 : 1;
        ref var score = ref HistoryScores[colorIndex, (int)move.From, (int)move.To];
        
        // Improved history update with better scaling
        var absBonus = Math.Abs(bonus);
        var scaledBonus = bonus - score * absBonus / 32768;
        score += scaledBonus;
        
        // Clamp to prevent overflow
        score = Math.Clamp(score, -32768, 32768);
    }
    
    public int GetHistoryScore(Move move, Color color)
    {
        if (move.IsCapture || move.IsPromotion)
            return 0;
            
        var colorIndex = color == Color.White ? 0 : 1;
        return HistoryScores[colorIndex, (int)move.From, (int)move.To];
    }
}