#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class MagicBitboardsTest
{
    [TestMethod]
    public void TestBishopAttacks()
    {
        // Test bishop on c1 with empty board
        var c1 = Square.C1;
        var emptyBoard = Bitboard.Empty;
        var attacks = MagicBitboards.GetBishopAttacks(c1, emptyBoard);
        
        Console.WriteLine($"Bishop on c1, empty board:");
        Console.WriteLine($"Attacks: {GetSquareList(attacks)}");
        Console.WriteLine($"Attack count: {Bitboard.PopCount(attacks)}");
        
        // Expected squares: d2, e3, f4, g5, h6, b2, a3
        var expectedSquares = new[] { Square.D2, Square.E3, Square.F4, Square.G5, Square.H6, Square.B2, Square.A3 };
        foreach (var sq in expectedSquares)
        {
            Assert.IsTrue((attacks & sq.ToBitboard()).IsNotEmpty(), $"Bishop from c1 should attack {sq.ToAlgebraic()}");
        }
        
        // Test with some blocking pieces
        var occupied = Square.E3.ToBitboard() | Square.B2.ToBitboard();
        attacks = MagicBitboards.GetBishopAttacks(c1, occupied);
        
        Console.WriteLine($"\nBishop on c1, with pieces on e3 and b2:");
        Console.WriteLine($"Attacks: {GetSquareList(attacks)}");
        
        // Should attack d2, e3 (blocker), b2 (blocker) but not beyond
        Assert.IsTrue((attacks & Square.D2.ToBitboard()).IsNotEmpty(), "Should attack d2");
        Assert.IsTrue((attacks & Square.E3.ToBitboard()).IsNotEmpty(), "Should attack e3 (blocker)");
        Assert.IsTrue((attacks & Square.B2.ToBitboard()).IsNotEmpty(), "Should attack b2 (blocker)");
        Assert.IsTrue((attacks & Square.F4.ToBitboard()).IsEmpty(), "Should NOT attack f4 (blocked)");
        Assert.IsTrue((attacks & Square.A3.ToBitboard()).IsEmpty(), "Should NOT attack a3 (blocked)");
    }
    
    [TestMethod]
    public void TestRookAttacks()
    {
        // Test rook on a1 with empty board
        var a1 = Square.A1;
        var emptyBoard = Bitboard.Empty;
        var attacks = MagicBitboards.GetRookAttacks(a1, emptyBoard);
        
        Console.WriteLine($"Rook on a1, empty board:");
        Console.WriteLine($"Attacks: {GetSquareList(attacks)}");
        Console.WriteLine($"Attack count: {Bitboard.PopCount(attacks)}");
        
        // Should attack entire file and rank
        Assert.AreEqual(14, Bitboard.PopCount(attacks), "Rook should attack 14 squares");
    }
    
    private string GetSquareList(Bitboard bb)
    {
        var squares = new List<string>();
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