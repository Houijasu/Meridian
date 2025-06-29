#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;

namespace Meridian.Tests.Perft;

[TestClass]
public class FailedDepthOnePositions
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void FindDepthOneFailures()
    {
        var testPositions = new (string fen, ulong expected, string description)[]
        {
            // Position 5 - fails at depth 1
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 5UL, "Position 5 - 3 pieces"),
            
            // Position 4 - fails at depth 1
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 6UL, "Position 4"),
            
            // Additional test positions
            ("8/8/8/8/8/8/8/R3K2r b Qk - 0 1", 16UL, "Black to move with castling"),
            ("8/8/8/8/8/8/8/r3k2R b Kq - 0 1", 16UL, "Black to move with castling 2"),
            ("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", 26UL, "Both sides can castle"),
            ("8/5bk1/8/2Pp4/8/1K6/8/8 w - d6 0 1", 8UL, "En passant position"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 15UL, "En passant position 2"),
            ("K7/8/8/3p4/4p3/8/8/7k b - - 0 1", 3UL, "Simple pawn moves"),
            ("k7/8/8/8/3P4/8/8/K7 w - - 0 1", 2UL, "White pawn move"),
            ("8/8/8/8/k2Pp2K/8/8/8 b - d3 0 1", 5UL, "En passant available"),
            ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 20UL, "Starting position"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 48UL, "Kiwipete")
        };
        
        Console.WriteLine("=== Testing Perft Depth 1 ===\n");
        
        var failures = new List<(string fen, ulong expected, ulong actual, string desc)>();
        
        Span<Move> moveBuffer = stackalloc Move[218];
        
        foreach (var (fen, expected, description) in testPositions)
        {
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine($"Failed to parse FEN: {fen}");
                continue;
            }
            
            var position = positionResult.Value;
            
            // Generate moves
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            var actual = (ulong)moves.Count;
            
            if (actual != expected)
            {
                failures.Add((fen, expected, actual, description));
                Console.WriteLine($"FAILED: {description}");
                Console.WriteLine($"  FEN: {fen}");
                Console.WriteLine($"  Expected: {expected} moves");
                Console.WriteLine($"  Actual: {actual} moves");
                Console.WriteLine($"  Difference: {(long)actual - (long)expected}\n");
                
                // List all moves for failed positions
                Console.WriteLine("  Generated moves:");
                for (int i = 0; i < moves.Count; i++)
                {
                    Console.WriteLine($"    {i+1}. {moves[i].ToUci()}");
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"PASSED: {description} - {actual} moves");
            }
        }
        
        Console.WriteLine("\n=== SUMMARY ===");
        Console.WriteLine($"Total positions tested: {testPositions.Length}");
        Console.WriteLine($"Failed: {failures.Count}");
        Console.WriteLine($"Passed: {testPositions.Length - failures.Count}");
        
        if (failures.Count > 0)
        {
            Console.WriteLine("\n=== Stockfish Comparison Commands ===");
            Console.WriteLine("Run these commands in Stockfish to compare:\n");
            
            foreach (var (fen, expected, actual, desc) in failures)
            {
                Console.WriteLine($"# {desc}");
                Console.WriteLine($"position fen {fen}");
                Console.WriteLine("go perft 1");
                Console.WriteLine($"# Expected: {expected}, Our engine: {actual}\n");
            }
        }
    }
}