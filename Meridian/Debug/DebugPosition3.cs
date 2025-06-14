using Meridian.Core;

namespace Meridian.Debug;

public static class DebugPosition3
{
    public static void RunDebug()
    {
        var board = FenParser.Parse("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
        
        Console.WriteLine("Debugging Position 3 at depth 2 (expected 191, got 193)");
        Console.WriteLine("Initial position:");
        PrintBoard(ref board);
        
        // Generate all moves at depth 1
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Console.WriteLine($"\nTotal moves at depth 1: {moves.Count}");
        
        // Process each move and count responses
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                MoveList responses = new();
                MoveGenerator.GenerateAllMoves(ref board, ref responses);
                
                int legalResponses = 0;
                for (int j = 0; j < responses.Count; j++)
                {
                    BoardState copy2 = board;
                    board.MakeMove(responses[j]);
                    
                    if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
                    {
                        legalResponses++;
                    }
                    
                    board = copy2;
                }
                
                Console.WriteLine($"{moves[i]}: {legalResponses} legal responses");
            }
            
            board = copy;
        }
    }
    
    private static void PrintBoard(ref BoardState board)
    {
        Console.WriteLine(FenParser.ToFen(ref board));
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}