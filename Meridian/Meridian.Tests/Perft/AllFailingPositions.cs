#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class AllFailingPositions
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void ReportAllFailingPositions()
    {
        var testPositions = new (string fen, int depth, ulong expected, string description)[]{
            // Standard positions
            (Position.StartingFen, 1, 20UL, "Starting position"),
            (Position.StartingFen, 2, 400UL, "Starting position"),
            (Position.StartingFen, 3, 8902UL, "Starting position"),
            (Position.StartingFen, 4, 197281UL, "Starting position"),
            
            // Kiwipete
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 1, 48UL, "Kiwipete"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 2, 2039UL, "Kiwipete"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 3, 97862UL, "Kiwipete"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 4, 4085603UL, "Kiwipete"),
            
            // Position 3
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 1, 14UL, "Position 3"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 2, 191UL, "Position 3"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 3, 2812UL, "Position 3"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 4, 43238UL, "Position 3"),
            
            // Position 4
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 1, 6UL, "Position 4"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 2, 264UL, "Position 4"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 3, 9467UL, "Position 4"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 4, 422333UL, "Position 4"),
            
            // Position 5 (minimal failing case)
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 1, 5UL, "Position 5 - 3 pieces"),
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 2, 75UL, "Position 5 - 3 pieces"),
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 3, 459UL, "Position 5 - 3 pieces"),
            ("r3k3/1K6/8/8/8/8/8/8 w q - 0 1", 4, 8290UL, "Position 5 - 3 pieces"),
            
            // Promotion position
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 1, 18UL, "Promotion position"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 2, 270UL, "Promotion position"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 3, 4699UL, "Promotion position"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 4, 73683UL, "Promotion position"),
            
            // En passant position
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 1, 15UL, "En passant position"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 2, 126UL, "En passant position"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 3, 1928UL, "En passant position"),
            ("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1", 4, 13931UL, "En passant position"),
            
            // Castling position
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 1, 26UL, "Castling position"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 2, 568UL, "Castling position"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 3, 13744UL, "Castling position"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 4, 314346UL, "Castling position"),
            
            // Position from CPW
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 1, 44UL, "CPW position"),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 2, 1486UL, "CPW position"),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 3, 62379UL, "CPW position"),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 4, 2103487UL, "CPW position")
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
        
        Console.WriteLine($"Total positions tested: {testPositions.Length}");
        Console.WriteLine($"Failed positions: {failures.Count}");
        Console.WriteLine($"Success rate: {(testPositions.Length - failures.Count) * 100.0 / testPositions.Length:F1}%");
        Console.WriteLine();
        
        if (failures.Count > 0)
        {
            Console.WriteLine("=== FAILED POSITIONS ===\n");
            
            // Group by position
            var grouped = failures.GroupBy(f => f.desc);
            
            foreach (var group in grouped)
            {
                Console.WriteLine($"{group.Key}:");
                var fen = group.First().fen;
                Console.WriteLine($"FEN: {fen}");
                
                foreach (var (_, depth, expected, actual, _) in group)
                {
                    var diff = (long)actual - (long)expected;
                    var percentage = Math.Abs(diff) * 100.0 / expected;
                    Console.WriteLine($"  Depth {depth}: {actual:N0} (expected {expected:N0}, diff: {diff:+#;-#;0}, {percentage:F2}% error)");
                }
                Console.WriteLine();
            }
        }
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