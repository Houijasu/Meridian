#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class FindSimplestFailures
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void FindFailuresWithFewerPieces()
    {
        var testPositions = new (string fen, int depth, ulong expected, string description)[]
        {
            // Positions with fewer pieces first
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 1, 14UL, "Position 3 - 11 pieces"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 2, 191UL, "Position 3 - 11 pieces"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 3, 2812UL, "Position 3 - 11 pieces"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 4, 43238UL, "Position 3 - 11 pieces"),
            
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 1, 5UL, "Position 5 - 3 pieces"),
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 2, 75UL, "Position 5 - 3 pieces"),
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 3, 459UL, "Position 5 - 3 pieces"),
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 4, 8290UL, "Position 5 - 3 pieces"),
            
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 1, 18UL, "Promotion - 7 pieces"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 2, 270UL, "Promotion - 7 pieces"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 3, 4699UL, "Promotion - 7 pieces"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 4, 73683UL, "Promotion - 7 pieces"),
            
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 1, 26UL, "Castling - 6 pieces"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 2, 568UL, "Castling - 6 pieces"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 3, 13744UL, "Castling - 6 pieces"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 4, 314346UL, "Castling - 6 pieces"),
            
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 1, 15UL, "En passant - 4 pieces"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 2, 126UL, "En passant - 4 pieces"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 3, 1928UL, "En passant - 4 pieces"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 4, 13931UL, "En passant - 4 pieces"),
            
            ("8/3K4/2p5/p2b2r1/5k2/8/8/1q6 b - - 1 67", 1, 50UL, "Complex endgame - 6 pieces"),
            ("8/3K4/2p5/p2b2r1/5k2/8/8/1q6 b - - 1 67", 2, 279UL, "Complex endgame - 6 pieces"),
        };
        
        var failures = new List<(string fen, int depth, ulong expected, ulong actual, string desc)>();
        
        foreach (var (fen, depth, expected, description) in testPositions)
        {
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine($"Failed to parse FEN: {fen}");
                continue;
            }
            
            var actual = Perft(positionResult.Value, depth);
            if (actual != expected)
            {
                failures.Add((fen, depth, expected, actual, description));
            }
        }
        
        // Sort by number of pieces (in description) and depth
        failures = failures.OrderBy(f => ExtractPieceCount(f.desc))
                          .ThenBy(f => f.depth)
                          .ToList();
        
        Console.WriteLine($"Found {failures.Count} failing positions:\n");
        
        foreach (var (fen, depth, expected, actual, desc) in failures)
        {
            var diff = (long)actual - (long)expected;
            var percentage = Math.Abs(diff) * 100.0 / expected;
            Console.WriteLine($"{desc}:");
            Console.WriteLine($"  FEN: {fen}");
            Console.WriteLine($"  Depth: {depth}");
            Console.WriteLine($"  Expected: {expected:N0}");
            Console.WriteLine($"  Actual: {actual:N0}");
            Console.WriteLine($"  Difference: {diff:+#;-#;0} ({percentage:F2}%)");
            Console.WriteLine();
        }
        
        if (failures.Count > 0)
        {
            Assert.Fail($"Found {failures.Count} perft failures");
        }
    }
    
    private int ExtractPieceCount(string description)
    {
        var parts = description.Split('-');
        if (parts.Length < 2) return 32;
        
        var piecePart = parts[1].Trim();
        if (int.TryParse(piecePart.Split(' ')[0], out var count))
            return count;
        
        return 32;
    }
    
    private ulong Perft(Position position, int depth)
    {
        if (depth == 0) return 1;
        
        ulong nodes = 0;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        for (int i = 0; i < moves.Count; i++)
        {
            var undoInfo = position.MakeMove(moves[i]);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(moves[i], undoInfo);
        }
        
        return nodes;
    }
}