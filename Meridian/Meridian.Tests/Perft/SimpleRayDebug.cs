#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Text;

namespace Meridian.Tests.Perft;

[TestClass]
public class SimpleRayDebug
{
    [TestMethod]
    public void DebugE8Southwest()
    {
        // E8 = square 60 (rank 7, file 4)
        // Direction array: { -9, -8, -7, -1, 1, 7, 8, 9 }
        // Index 5 (SouthWest) = offset 7
        
        var output = new StringBuilder();
        
        // Manually trace what should happen
        var square = 60; // E8
        var directions = new[] { -9, -8, -7, -1, 1, 7, 8, 9 };
        var offset = directions[AttackTables.Directions.SouthWest];  // Should be 7
        
        output.AppendLine($"Starting at square {square} (E8)");
        output.AppendLine($"Direction offset: {offset}");
        
        var current = square;
        int steps = 0;
        
        while (steps < 10)
        {
            var prev = current;
            current += offset;
            
            output.AppendLine($"\nStep {steps + 1}:");
            output.AppendLine($"  Previous square: {prev}");
            output.AppendLine($"  Current square: {current}");
            
            if (current < 0 || current >= 64)
            {
                output.AppendLine("  Out of bounds - break");
                break;
            }
            
            var prevFile = prev & 7;
            var newFile = current & 7;
            var prevRank = prev >> 3;
            var newRank = current >> 3;
            
            output.AppendLine($"  Prev: file={prevFile}, rank={prevRank}");
            output.AppendLine($"  New: file={newFile}, rank={newRank}");
            
            var fileDiff = Math.Abs(newFile - prevFile);
            var rankDiff = Math.Abs(newRank - prevRank);
            
            output.AppendLine($"  FileDiff: {fileDiff}, RankDiff: {rankDiff}");
            
            if (fileDiff > 1 || rankDiff > 1)
            {
                output.AppendLine("  Wrap-around detected - break");
                break;
            }
            
            output.AppendLine("  Valid square - continue");
            steps++;
        }
        
        output.AppendLine($"\nTotal steps: {steps}");
        
        // Check actual ray
        var ray = AttackTables.GetRay(Square.E8, AttackTables.Directions.SouthWest);
        output.AppendLine($"\nActual ray popcount: {Bitboard.PopCount(ray)}");
        
        System.IO.File.WriteAllText("/tmp/ray_debug_simple.txt", output.ToString());
        
        Console.WriteLine("Debug output written to /tmp/ray_debug_simple.txt");
        Console.WriteLine($"Ray popcount: {Bitboard.PopCount(ray)}");
        
        Assert.Fail("Check /tmp/ray_debug_simple.txt for debug output");
    }
}