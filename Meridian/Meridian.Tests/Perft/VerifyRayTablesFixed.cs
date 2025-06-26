#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class VerifyRayTablesFixed
{
    [TestMethod]
    public void VerifyRaysWork()
    {
        Console.WriteLine("Testing E8 Southwest ray:");
        var ray = AttackTables.GetRay(Square.E8, AttackTables.Directions.SouthWest);
        var popcount = Bitboard.PopCount(ray);
        Console.WriteLine($"Popcount: {popcount}");
        
        // E8 southwest should hit D7, C6, B5, A4
        Assert.IsTrue(popcount > 0, "E8 southwest ray should not be empty");
        
        // Test a few more rays
        Console.WriteLine("\nTesting various rays:");
        
        // A1 northeast should hit B2, C3, D4, E5, F6, G7, H8
        var a1ne = AttackTables.GetRay(Square.A1, AttackTables.Directions.NorthEast);
        Console.WriteLine($"A1 NorthEast popcount: {Bitboard.PopCount(a1ne)}");
        Assert.AreEqual(7, Bitboard.PopCount(a1ne));
        
        // E4 in all directions
        Console.WriteLine("\nE4 rays in all directions:");
        for (int dir = 0; dir < 8; dir++)
        {
            var e4ray = AttackTables.GetRay(Square.E4, dir);
            var count = Bitboard.PopCount(e4ray);
            var dirName = dir switch
            {
                0 => "NorthWest",
                1 => "North",
                2 => "NorthEast", 
                3 => "West",
                4 => "East",
                5 => "SouthWest",
                6 => "South",
                7 => "SouthEast",
                _ => "Unknown"
            };
            Console.WriteLine($"  {dirName}: {count} squares");
            Assert.IsTrue(count > 0, $"E4 {dirName} ray should not be empty");
        }
        
        Console.WriteLine("\nAll ray tests passed!");
    }
}