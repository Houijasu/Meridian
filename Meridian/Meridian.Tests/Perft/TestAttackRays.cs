#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestAttackRays
{
    [TestMethod]
    public void TestE8RayToSouthWest()
    {
        // Test ray from E8 going southwest
        var from = Square.E8;
        var direction = AttackTables.Directions.SouthWest;
        
        var ray = AttackTables.GetRay(from, direction);
        
        Console.WriteLine($"Ray from {from} going SouthWest:");
        PrintBitboard(ray);
        
        // Expected squares: D7, C6, B5, A4
        var expectedSquares = new[] { Square.D7, Square.C6, Square.B5, Square.A4 };
        
        foreach (var square in expectedSquares)
        {
            var contains = (ray & square.ToBitboard()).IsNotEmpty();
            Console.WriteLine($"Ray contains {square}: {contains}");
        }
        
        // Write to file
        var output = new System.Text.StringBuilder();
        output.AppendLine($"Ray from {from} going SouthWest:");
        output.AppendLine($"Popcount: {Bitboard.PopCount(ray)}");
        output.AppendLine($"Contains D7: {(ray & Square.D7.ToBitboard()).IsNotEmpty()}");
        output.AppendLine($"Contains C6: {(ray & Square.C6.ToBitboard()).IsNotEmpty()}");
        output.AppendLine($"Contains B5: {(ray & Square.B5.ToBitboard()).IsNotEmpty()}");
        System.IO.File.WriteAllText("/tmp/e8_southwest_ray.txt", output.ToString());
        
        Assert.IsTrue(Bitboard.PopCount(ray) > 0, "Ray should not be empty");
    }
    
    [TestMethod]
    public void TestB5RayToNorthEast()
    {
        // Test ray from B5 going northeast
        var from = Square.B5;
        var direction = AttackTables.Directions.NorthEast;
        
        var ray = AttackTables.GetRay(from, direction);
        
        Console.WriteLine($"Ray from {from} going NorthEast:");
        PrintBitboard(ray);
        
        // Expected squares: C6, D7, E8
        var expectedSquares = new[] { Square.C6, Square.D7, Square.E8 };
        
        foreach (var square in expectedSquares)
        {
            var contains = (ray & square.ToBitboard()).IsNotEmpty();
            Console.WriteLine($"Ray contains {square}: {contains}");
        }
        
        Assert.IsTrue(Bitboard.PopCount(ray) > 0, "Ray should not be empty");
    }
    
    private void PrintBitboard(Bitboard bb)
    {
        for (int rank = 7; rank >= 0; rank--)
        {
            Console.Write($"{rank + 1} ");
            for (int file = 0; file < 8; file++)
            {
                var square = SquareExtensions.FromFileRank(file, rank);
                var hasSquare = (bb & square.ToBitboard()).IsNotEmpty();
                Console.Write(hasSquare ? "X " : ". ");
            }
            Console.WriteLine();
        }
        Console.WriteLine("  a b c d e f g h");
    }
}