using Meridian.Core;

namespace Meridian.Debug;

public static class ListAllMoves
{
    public static void ShowPosition3Moves()
    {
        var board = FenParser.Parse("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
        
        Console.WriteLine("Position 3 - All legal moves:");
        Console.WriteLine("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
        Console.WriteLine();
        
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Console.WriteLine($"Generated {moves.Count} moves:");
        for (int i = 0; i < moves.Count; i++)
        {
            Console.WriteLine($"{i+1}. {moves[i]}");
        }
        
        // Check for missing king moves
        Console.WriteLine("\nKing position:");
        var kingSquare = (Square)Bitboard.BitScanForward(board.WhiteKing);
        Console.WriteLine($"White king at: {kingSquare}");
        
        // Manual check for all possible king moves
        Console.WriteLine("\nExpected king moves from a5:");
        Console.WriteLine("- a4 (down)");
        Console.WriteLine("- a6 (up)");
        Console.WriteLine("- b4 (right) - blocked by rook");
        Console.WriteLine("- b5 (up-right) - has pawn");
        Console.WriteLine("- b6 (up-right from a5)");
        
        // Check what king moves we're generating
        Console.WriteLine("\nGenerated king moves:");
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == kingSquare)
            {
                Console.WriteLine($"- {moves[i]}");
            }
        }
    }
}