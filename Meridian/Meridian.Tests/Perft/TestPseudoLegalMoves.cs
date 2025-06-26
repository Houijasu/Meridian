#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestPseudoLegalMoves
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CheckThreePiecePosition()
    {
        // This position expects 5 moves but we generate 4
        var positionResult = Position.FromFen("r3k3/1K6/8/8/8/8/8/8 w q - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine($"White king on b7, Black rook on a8");
        Console.WriteLine();
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Generated {moves.Count} moves:");
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            Console.WriteLine($"{i+1}. {move.ToUci()}");
        }
        
        Console.WriteLine("\nExpected moves:");
        var expectedMoves = new[] { "b7a6", "b7a7", "b7a8", "b7b6", "b7b8", "b7c6", "b7c7", "b7c8" };
        foreach (var move in expectedMoves)
        {
            var found = false;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].ToUci() == move)
                {
                    found = true;
                    break;
                }
            }
            Console.WriteLine($"{move}: {(found ? "✓" : "✗ MISSING")}");
        }
        
        // Check if squares are attacked
        Console.WriteLine("\nSquares under attack by black rook on a8:");
        var rookOn = Square.A8;
        var occupied = position.OccupiedSquares();
        
        // Get rook attacks using ray attacks
        var north = AttackTables.GetRay(rookOn, AttackTables.Directions.North) & occupied;
        var south = AttackTables.GetRay(rookOn, AttackTables.Directions.South) & occupied;
        var east = AttackTables.GetRay(rookOn, AttackTables.Directions.East) & occupied;
        var west = AttackTables.GetRay(rookOn, AttackTables.Directions.West) & occupied;
        
        Console.WriteLine("Rook can attack along rank 8 and file a");
        
        Console.WriteLine("\nThe issue is that we're not generating moves to attacked squares.");
        Console.WriteLine("Standard perft counts pseudo-legal moves (including moves into check).");
        Console.WriteLine("Our implementation generates only legal moves, filtering out moves to attacked squares.");
    }
    
    private string GetSquareList(Bitboard bb)
    {
        var squares = new System.Collections.Generic.List<string>();
        var temp = bb;
        while (temp.IsNotEmpty())
        {
            var sq = (Square)temp.GetLsbIndex();
            squares.Add(sq.ToAlgebraic());
            temp = temp.RemoveLsb();
        }
        return string.Join(", ", squares);
    }
}