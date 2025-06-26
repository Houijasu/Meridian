#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugGetRayBetween
{
    [TestMethod]
    public void DebugRayCalculation()
    {
        var from = Square.E8;  // King at e8
        var to = Square.B5;    // Bishop at b5
        
        var fileDiff = to.File() - from.File();  // 1 - 4 = -3
        var rankDiff = to.Rank() - from.Rank();  // 4 - 7 = -3
        
        Console.WriteLine($"From: {from} (file={from.File()}, rank={from.Rank()})");
        Console.WriteLine($"To: {to} (file={to.File()}, rank={to.Rank()})");
        Console.WriteLine($"FileDiff: {fileDiff}");
        Console.WriteLine($"RankDiff: {rankDiff}");
        Console.WriteLine($"Abs(FileDiff): {Math.Abs(fileDiff)}");
        Console.WriteLine($"Abs(RankDiff): {Math.Abs(rankDiff)}");
        
        // Check if they're on same diagonal
        var onSameDiagonal = Math.Abs(fileDiff) == Math.Abs(rankDiff);
        Console.WriteLine($"On same diagonal: {onSameDiagonal}");
        
        if (onSameDiagonal)
        {
            // Determine direction from 'from' to 'to'
            int direction;
            if (fileDiff == rankDiff)
            {
                direction = fileDiff > 0 ? AttackTables.Directions.NorthEast : AttackTables.Directions.SouthWest;
                Console.WriteLine($"fileDiff == rankDiff, direction: {(fileDiff > 0 ? "NorthEast" : "SouthWest")}");
            }
            else // fileDiff == -rankDiff
            {
                direction = fileDiff > 0 ? AttackTables.Directions.SouthEast : AttackTables.Directions.NorthWest;
                Console.WriteLine($"fileDiff == -rankDiff, direction: {(fileDiff > 0 ? "SouthEast" : "NorthWest")}");
            }
            
            // Get ray from 'from' in the direction
            var ray1 = AttackTables.GetRay(from, direction);
            Console.WriteLine($"\nRay from {from} in direction {direction}:");
            PrintBitboard(ray1);
            
            // Get opposite direction
            var oppositeDirection = GetOppositeDirection(direction);
            Console.WriteLine($"\nOpposite direction: {oppositeDirection}");
            
            // Get ray from 'to' in opposite direction
            var ray2 = AttackTables.GetRay(to, oppositeDirection);
            Console.WriteLine($"\nRay from {to} in direction {oppositeDirection}:");
            PrintBitboard(ray2);
            
            // Intersection
            var intersection = ray1 & ray2;
            Console.WriteLine($"\nIntersection:");
            PrintBitboard(intersection);
            
            // Check specific squares
            Console.WriteLine($"\nChecking specific squares:");
            Console.WriteLine($"Ray1 contains C6: {(ray1 & Square.C6.ToBitboard()).IsNotEmpty()}");
            Console.WriteLine($"Ray1 contains D7: {(ray1 & Square.D7.ToBitboard()).IsNotEmpty()}");
            Console.WriteLine($"Ray2 contains C6: {(ray2 & Square.C6.ToBitboard()).IsNotEmpty()}");
            Console.WriteLine($"Ray2 contains D7: {(ray2 & Square.D7.ToBitboard()).IsNotEmpty()}");
            
            // Write to file for debugging
            var output = new System.Text.StringBuilder();
            output.AppendLine($"Ray from {from} in direction {direction}: popcount={Bitboard.PopCount(ray1)}");
            output.AppendLine($"Ray from {to} in direction {oppositeDirection}: popcount={Bitboard.PopCount(ray2)}");
            output.AppendLine($"Intersection popcount: {Bitboard.PopCount(intersection)}");
            System.IO.File.WriteAllText("/tmp/ray_debug.txt", output.ToString());
        }
    }
    
    private int GetOppositeDirection(int direction)
    {
        return direction switch
        {
            AttackTables.Directions.NorthWest => AttackTables.Directions.SouthEast,
            AttackTables.Directions.North => AttackTables.Directions.South,
            AttackTables.Directions.NorthEast => AttackTables.Directions.SouthWest,
            AttackTables.Directions.West => AttackTables.Directions.East,
            AttackTables.Directions.East => AttackTables.Directions.West,
            AttackTables.Directions.SouthWest => AttackTables.Directions.NorthEast,
            AttackTables.Directions.South => AttackTables.Directions.North,
            AttackTables.Directions.SouthEast => AttackTables.Directions.NorthWest,
            _ => direction
        };
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