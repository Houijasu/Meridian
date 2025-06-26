#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestDirectionMapping
{
    [TestMethod]
    public void TestDirectionOffsets()
    {
        // Direction constants
        var directionNames = new[] { "NorthWest", "North", "NorthEast", "West", "East", "SouthWest", "South", "SouthEast" };
        var directionOffsets = new[] { -9, -8, -7, -1, 1, 7, 8, 9 };
        
        Console.WriteLine("Direction mappings:");
        for (int i = 0; i < 8; i++)
        {
            Console.WriteLine($"{i}: {directionNames[i]} = offset {directionOffsets[i]}");
        }
        
        // Test specific case: E8 going southwest
        var e8 = 60; // Square E8 = rank 7 * 8 + file 4 = 60
        var southwestOffset = directionOffsets[AttackTables.Directions.SouthWest];
        
        Console.WriteLine($"\nE8 (square {e8}) going SouthWest (offset {southwestOffset}):");
        
        var current = e8;
        for (int step = 0; step < 10; step++)
        {
            current += southwestOffset;
            if (current < 0 || current >= 64) break;
            
            var file = current & 7;
            var rank = current >> 3;
            var square = SquareExtensions.FromFileRank(file, rank);
            
            Console.WriteLine($"Step {step + 1}: square {current} = {square} (file {file}, rank {rank})");
            
            // Check wrap-around
            var prevFile = (current - southwestOffset) & 7;
            var prevRank = (current - southwestOffset) >> 3;
            var fileDiff = Math.Abs(file - prevFile);
            var rankDiff = Math.Abs(rank - prevRank);
            
            Console.WriteLine($"  FileDiff: {fileDiff}, RankDiff: {rankDiff}");
            
            if (fileDiff > 1 || rankDiff > 1)
            {
                Console.WriteLine("  Would break due to wrap-around check!");
                break;
            }
        }
        
        // Also check what AttackTables.GetRay returns
        var ray = AttackTables.GetRay(Square.E8, AttackTables.Directions.SouthWest);
        Console.WriteLine($"\nAttackTables.GetRay(E8, SouthWest) popcount: {Bitboard.PopCount(ray)}");
    }
}