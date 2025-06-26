#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class Debug3PiecePosition
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void DebugThreePiecePosition()
    {
        // Position with 3 pieces - expecting 5 moves but getting 4
        var positionResult = Position.FromFen("r3k3/1K6/8/8/8/8/8/8 w q - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine($"Side to move: {position.SideToMove}");
        Console.WriteLine($"Castling rights: {position.CastlingRights}");
        Console.WriteLine();
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Generated {moves.Count} moves (expected 5):");
        
        // List all moves
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            Console.WriteLine($"{i+1}. {move.ToUci()} (flags: {move.Flags})");
        }
        
        // Expected moves for white king on b7:
        Console.WriteLine("\nExpected moves:");
        var expectedSquares = new[] { "a8", "a7", "a6", "b8", "b6", "c8", "c7", "c6" };
        var kingSquare = Square.B7;
        
        foreach (var sq in expectedSquares)
        {
            var targetSquare = SquareExtensions.ParseSquare(sq);
            var found = false;
            
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].From == kingSquare && moves[i].To == targetSquare)
                {
                    found = true;
                    break;
                }
            }
            
            Console.WriteLine($"King to {sq}: {(found ? "✓" : "✗ MISSING")}");
        }
        
        // Now let's switch sides and check black's moves
        Console.WriteLine("\n--- Black's turn ---");
        var blackTurn = Position.FromFen("r3k3/1K6/8/8/8/8/8/8 b q - 0 1");
        Assert.IsTrue(blackTurn.IsSuccess);
        
        Span<Move> blackMoves = stackalloc Move[218];
        var blackList = new MoveList(blackMoves);
        _moveGenerator.GenerateMoves(blackTurn.Value, ref blackList);
        
        Console.WriteLine($"\nBlack has {blackList.Count} moves:");
        
        var castlingFound = false;
        for (int i = 0; i < blackList.Count; i++)
        {
            var move = blackList[i];
            Console.WriteLine($"{i+1}. {move.ToUci()} (flags: {move.Flags})");
            
            if ((move.Flags & MoveType.Castling) != 0)
            {
                castlingFound = true;
                Console.WriteLine("   ^ This is castling!");
            }
        }
        
        Console.WriteLine($"\nBlack castling O-O-O found: {castlingFound}");
    }
}