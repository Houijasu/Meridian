#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class VerifyDirectionOffsets
{
    [TestMethod]
    public void VerifyOffsets()
    {
        // From any square in the middle of the board, calculate what the offsets should be
        var testSquare = Square.E4; // Square 28 (rank 3, file 4)
        var testIndex = 28;
        
        Console.WriteLine($"Test square: {testSquare} (index {testIndex})");
        Console.WriteLine("\nExpected offsets for each direction:");
        
        // Calculate what each direction should be
        // North: up one rank = +8
        // South: down one rank = -8
        // East: right one file = +1
        // West: left one file = -1
        // NorthEast: up one rank, right one file = +8 + 1 = +9
        // NorthWest: up one rank, left one file = +8 - 1 = +7
        // SouthEast: down one rank, right one file = -8 + 1 = -7
        // SouthWest: down one rank, left one file = -8 - 1 = -9
        
        Console.WriteLine("North: +8");
        Console.WriteLine("South: -8");
        Console.WriteLine("East: +1");
        Console.WriteLine("West: -1");
        Console.WriteLine("NorthEast: +9");
        Console.WriteLine("NorthWest: +7");
        Console.WriteLine("SouthEast: -7");
        Console.WriteLine("SouthWest: -9");
        
        Console.WriteLine("\nActual direction constants:");
        Console.WriteLine($"NorthWest = {AttackTables.Directions.NorthWest}");
        Console.WriteLine($"North = {AttackTables.Directions.North}");
        Console.WriteLine($"NorthEast = {AttackTables.Directions.NorthEast}");
        Console.WriteLine($"West = {AttackTables.Directions.West}");
        Console.WriteLine($"East = {AttackTables.Directions.East}");
        Console.WriteLine($"SouthWest = {AttackTables.Directions.SouthWest}");
        Console.WriteLine($"South = {AttackTables.Directions.South}");
        Console.WriteLine($"SouthEast = {AttackTables.Directions.SouthEast}");
        
        Console.WriteLine("\nDirection array used in InitializeRayAttacks:");
        var directions = new[] { -9, -8, -7, -1, 1, 7, 8, 9 };
        for (int i = 0; i < 8; i++)
        {
            Console.WriteLine($"Index {i}: offset {directions[i]}");
        }
        
        Console.WriteLine("\nPROBLEM FOUND:");
        Console.WriteLine("The direction array doesn't match the expected offsets!");
        Console.WriteLine("Index 0 has offset -9, which is SouthWest, but constant NorthWest = 0");
        Console.WriteLine("Index 5 has offset 7, which is NorthWest, but constant SouthWest = 5");
        
        Assert.Fail("Direction offsets don't match direction constants!");
    }
}