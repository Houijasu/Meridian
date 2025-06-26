#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Reflection;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestGetRayBetween
{
    [TestMethod]
    public void TestRayBetweenE8B5()
    {
        // Get the private GetRayBetween method
        var getRayBetweenMethod = typeof(MoveGenerator).GetMethod("GetRayBetween", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        // Test ray between E8 and B5
        var ray = (Bitboard)getRayBetweenMethod!.Invoke(null, new object[] { Square.E8, Square.B5 })!;
        
        Console.WriteLine($"Ray between E8 and B5: 0x{ray:X16}");
        
        // Show squares in the ray
        Console.WriteLine("Squares in ray:");
        var rayCopy = ray;
        while (rayCopy.IsNotEmpty())
        {
            var sq = (Square)rayCopy.GetLsbIndex();
            Console.WriteLine($"  {sq}");
            rayCopy = rayCopy.RemoveLsb();
        }
        
        // Expected squares: C6, D7
        var c6InRay = (ray & Square.C6.ToBitboard()).IsNotEmpty();
        var d7InRay = (ray & Square.D7.ToBitboard()).IsNotEmpty();
        
        Console.WriteLine($"\nC6 in ray: {c6InRay}");
        Console.WriteLine($"D7 in ray: {d7InRay}");
        
        // Let's also test GetRay from AttackTables
        var getRayMethod = typeof(AttackTables).GetMethod("GetRay", 
            BindingFlags.Public | BindingFlags.Static);
        
        // Southwest from E8
        var swDirection = 5; // Based on the Directions enum in the code
        var rayFromE8 = (Bitboard)getRayMethod!.Invoke(null, new object[] { Square.E8, swDirection })!;
        
        Console.WriteLine($"\nRay from E8 southwest: 0x{rayFromE8:X16}");
        var rayFromE8Copy = rayFromE8;
        while (rayFromE8Copy.IsNotEmpty())
        {
            var sq = (Square)rayFromE8Copy.GetLsbIndex();
            Console.WriteLine($"  {sq}");
            rayFromE8Copy = rayFromE8Copy.RemoveLsb();
        }
        
        // Northeast from B5
        var neDirection = 1; // Opposite of southwest
        var rayFromB5 = (Bitboard)getRayMethod!.Invoke(null, new object[] { Square.B5, neDirection })!;
        
        Console.WriteLine($"\nRay from B5 northeast: 0x{rayFromB5:X16}");
        var rayFromB5Copy = rayFromB5;
        while (rayFromB5Copy.IsNotEmpty())
        {
            var sq = (Square)rayFromB5Copy.GetLsbIndex();
            Console.WriteLine($"  {sq}");
            rayFromB5Copy = rayFromB5Copy.RemoveLsb();
        }
        
        // The intersection
        var intersection = rayFromE8 & rayFromB5;
        Console.WriteLine($"\nIntersection: 0x{intersection:X16}");
    }
}