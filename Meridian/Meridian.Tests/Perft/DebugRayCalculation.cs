#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugRayCalculation
{
    [TestMethod]
    public void DebugE8ToB5Ray()
    {
        // E8 = square 60, B5 = square 33
        var e8 = Square.E8;
        var b5 = Square.B5;
        
        Console.WriteLine($"E8: square {(int)e8}, file {e8.File()}, rank {e8.Rank()}");
        Console.WriteLine($"B5: square {(int)b5}, file {b5.File()}, rank {b5.Rank()}");
        
        // The diagonal from E8 to B5 should include D7 and C6
        // E8 (60) -> D7 (51) -> C6 (42) -> B5 (33)
        var d7 = Square.D7;
        var c6 = Square.C6;
        
        Console.WriteLine($"D7: square {(int)d7}, file {d7.File()}, rank {d7.Rank()}");
        Console.WriteLine($"C6: square {(int)c6}, file {c6.File()}, rank {c6.Rank()}");
        
        // Let's manually check the offsets
        Console.WriteLine($"\nSquare differences:");
        Console.WriteLine($"E8 to D7: {(int)d7 - (int)e8} (should be -9 for SW)");
        Console.WriteLine($"D7 to C6: {(int)c6 - (int)d7} (should be -9 for SW)");
        Console.WriteLine($"C6 to B5: {(int)b5 - (int)c6} (should be -9 for SW)");
        
        // Now let's test all 8 directions from E8
        var directions = new[] { "NW", "N", "NE", "W", "E", "SW", "S", "SE" };
        var offsets = new[] { -9, -8, -7, -1, 1, 7, 8, 9 };
        
        Console.WriteLine($"\nRays from E8 in all directions:");
        for (int dir = 0; dir < 8; dir++)
        {
            var ray = AttackTables.GetRay(e8, dir);
            Console.WriteLine($"{directions[dir]} (offset {offsets[dir]}): 0x{ray:X16}");
            
            // Show first few squares in the ray
            var rayCopy = ray;
            var count = 0;
            while (rayCopy.IsNotEmpty() && count < 3)
            {
                var sq = (Square)rayCopy.GetLsbIndex();
                Console.WriteLine($"  {sq} (square {(int)sq})");
                rayCopy = rayCopy.RemoveLsb();
                count++;
            }
        }
        
        // The issue might be with how rays are calculated for edge squares
        // Let's check what ray we get from D7 southwest
        Console.WriteLine($"\nRay from D7 southwest:");
        var rayFromD7 = AttackTables.GetRay(d7, AttackTables.Directions.SouthWest);
        Console.WriteLine($"0x{rayFromD7:X16}");
        var rayFromD7Copy = rayFromD7;
        while (rayFromD7Copy.IsNotEmpty())
        {
            var sq = (Square)rayFromD7Copy.GetLsbIndex();
            Console.WriteLine($"  {sq}");
            rayFromD7Copy = rayFromD7Copy.RemoveLsb();
        }
    }
}