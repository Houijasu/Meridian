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
        var pvIndex = ply * SearchConstants.MaxDepth;
        PvTable[pvIndex + ply] = move;
        
        // Copy the rest of the PV from ply+1
        var nextPvIndex = (ply + 1) * SearchConstants.MaxDepth;
        for (var i = ply + 1; i < PvLength[ply + 1]; i++)
        {
            PvTable[pvIndex + i] = PvTable[nextPvIndex + i];
        }
        
        PvLength[ply] = PvLength[ply + 1] + 1;
        
        // Update the root PV for display
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
    
    public Move GetPvMove(int ply)
    {
        if (ply >= SearchConstants.MaxDepth || ply >= PvLength[0])
            return Move.None;
            
        // For ply 0, the PV starts at index 0
        // For subsequent plies, we use the PV from the root
        return PvTable[ply];
    }
    
    public bool IsPvMove(Move move, int ply)
    {
        return move == GetPvMove(ply);
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