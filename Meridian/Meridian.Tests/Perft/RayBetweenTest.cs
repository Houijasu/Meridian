#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Reflection;

namespace Meridian.Tests.Perft;

[TestClass]
public class RayBetweenTest
{
    [TestMethod]
    public void TestGetRayBetween()
    {
        // Test the ray between B5 (checking bishop) and E8 (black king)
        var from = Square.E8;  // King
        var to = Square.B5;    // Bishop
        
        // Use reflection to call the private GetRayBetween method
        var moveGeneratorType = typeof(MoveGenerator);
        var getRayBetweenMethod = moveGeneratorType.GetMethod("GetRayBetween", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        Assert.IsNotNull(getRayBetweenMethod);
        
        var ray = (Bitboard)getRayBetweenMethod.Invoke(null, new object[] { from, to })!;
        
        Console.WriteLine($"Ray between {from} (king) and {to} (bishop):");
        PrintBitboard(ray);
        
        // The ray should include C6 and D7
        var expectedSquares = new[] { Square.C6, Square.D7 };
        
        foreach (var square in expectedSquares)
        {
            var contains = (ray & square.ToBitboard()).IsNotEmpty();
            Console.WriteLine($"Ray contains {square}: {contains}");
            if (!contains)
            {
                Assert.Fail($"Ray should contain {square} but doesn't");
            }
        }
        
        // Also test from bishop to king
        var ray2 = (Bitboard)getRayBetweenMethod.Invoke(null, new object[] { to, from })!;
        Console.WriteLine($"\nRay between {to} (bishop) and {from} (king):");
        PrintBitboard(ray2);
        
        // Should be the same
        Assert.AreEqual(ray, ray2, "Ray should be symmetric");
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