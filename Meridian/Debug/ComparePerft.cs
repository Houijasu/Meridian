using Meridian.Core;

namespace Meridian.Debug;

public static class ComparePerft
{
    // Known correct perft values for Position 3 at depth 2
    private static readonly Dictionary<string, int> KnownPerft = new()
    {
        { "e2e3", 14 },
        { "e2e4", 16 },
        { "g2g3", 3 },
        { "g2g4", 16 },
        { "b4b1", 16 },
        { "b4b2", 16 },
        { "b4b3", 15 },
        { "b4a4", 15 },
        { "b4c4", 15 },
        { "b4d4", 15 },
        { "b4e4", 15 },
        { "b4f4", 2 },
        { "a5a4", 15 },
        { "a5a6", 15 },
        { "a5b4", 15 },
        { "a5b5", 17 },
        { "a5b6", 16 }
    };
    
    public static void ComparePosition3()
    {
        var board = FenParser.Parse("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
        
        Console.WriteLine("Comparing Position 3 perft at depth 2:");
        Console.WriteLine("Move    | Expected | Actual | Diff");
        Console.WriteLine("--------|----------|--------|-----");
        
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        int totalExpected = 0;
        int totalActual = 0;
        
        foreach (var kvp in KnownPerft)
        {
            totalExpected += kvp.Value;
        }
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                string moveStr = moves[i].ToString();
                int nodes = CountLegalMoves(ref board);
                totalActual += nodes;
                
                if (KnownPerft.TryGetValue(moveStr, out int expected))
                {
                    int diff = nodes - expected;
                    string status = diff == 0 ? "OK" : "***";
                    Console.WriteLine($"{moveStr,-7} | {expected,8} | {nodes,6} | {diff,4} {status}");
                }
                else
                {
                    Console.WriteLine($"{moveStr,-7} | UNKNOWN  | {nodes,6} | ??? ***");
                }
            }
            
            board = copy;
        }
        
        Console.WriteLine("--------|----------|--------|-----");
        Console.WriteLine($"TOTAL   | {totalExpected,8} | {totalActual,6} | {totalActual - totalExpected,4}");
    }
    
    private static int CountLegalMoves(ref BoardState board)
    {
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        int legalCount = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                legalCount++;
            }
            
            board = copy;
        }
        
        return legalCount;
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}