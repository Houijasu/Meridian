using Meridian.Core;

namespace Meridian.Debug;

public static class TestMoveComparison
{
    public static void TestCastleMoveDetection()
    {
        Console.WriteLine("Testing castle move detection:\n");
        
        // Create castle moves
        var moves = new Move[]
        {
            new Move(Square.E1, Square.G1, MoveType.Castle),
            new Move(Square.E1, Square.C1, MoveType.Castle),
            new Move(Square.E8, Square.G8, MoveType.Castle),
            new Move(Square.E8, Square.C8, MoveType.Castle),
            new Move(Square.E8, Square.D8, MoveType.Normal),
            new Move(Square.E8, Square.F8, MoveType.Normal)
        };
        
        foreach (var move in moves)
        {
            Console.WriteLine($"Move: {move}");
            Console.WriteLine($"  Type: {move.Type}");
            Console.WriteLine($"  Type == Castle: {move.Type == MoveType.Castle}");
            Console.WriteLine($"  IsCastle(): {move.IsCastle()}");
            Console.WriteLine();
        }
        
        // Now test in a MoveList
        Console.WriteLine("Testing in MoveList:");
        MoveList moveList = new();
        foreach (var move in moves)
        {
            moveList.Add(move);
        }
        
        Console.WriteLine($"Added {moveList.Count} moves to list");
        
        int castleCount = 0;
        for (int i = 0; i < moveList.Count; i++)
        {
            var move = moveList[i];
            Console.WriteLine($"[{i}] {move} - Type: {move.Type}");
            if (move.Type == MoveType.Castle)
            {
                castleCount++;
            }
        }
        
        Console.WriteLine($"\nTotal castle moves found: {castleCount}");
    }
}