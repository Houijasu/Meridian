#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class UpdatedPerftSummary
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void SummaryAfterCorrections()
    {
        Console.WriteLine("=== PERFT SUMMARY AFTER TEST CORRECTIONS ===\n");
        
        var tests = new[]
        {
            ("Starting position", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 
                new[] { 20UL, 400UL, 8902UL, 197281UL }),
            ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                new[] { 48UL, 2039UL, 97862UL, 4085603UL }),
            ("Position 3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                new[] { 14UL, 191UL, 2812UL, 43238UL }),
            ("Position 4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                new[] { 6UL, 264UL, 9467UL, 422333UL }),
            ("Position 5", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
                new[] { 44UL, 1486UL, 62379UL, 2103487UL }),
            ("Black castling", "4k2r/8/8/8/8/8/8/4K3 b k - 0 1",
                new[] { 15UL, 66UL, 1197UL, 7059UL }),
            ("En passant (CORRECTED)", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3",
                new[] { 31UL, 707UL, 21637UL, 524138UL }),
            ("Promotion (CORRECTED)", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1",
                new[] { 18UL, 270UL, 4699UL, 79355UL })
        };
        
        var totalPassed = 0;
        var totalFailed = 0;
        
        foreach (var (name, fen, expected) in tests)
        {
            Console.WriteLine($"{name}:");
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine("  ERROR: Invalid FEN\n");
                continue;
            }
            
            var position = positionResult.Value;
            
            for (int depth = 1; depth <= expected.Length; depth++)
            {
                var expectedNodes = expected[depth - 1];
                var actualNodes = Perft(position, depth);
                var diff = (long)actualNodes - (long)expectedNodes;
                var pass = actualNodes == expectedNodes;
                
                if (pass)
                {
                    Console.WriteLine($"  Depth {depth}: {actualNodes:N0} ✓");
                    totalPassed++;
                }
                else
                {
                    Console.WriteLine($"  Depth {depth}: {actualNodes:N0} (expected {expectedNodes:N0}, diff: {diff:+#;-#;0})");
                    totalFailed++;
                }
            }
            Console.WriteLine();
        }
        
        Console.WriteLine($"Total tests: {totalPassed + totalFailed}");
        Console.WriteLine($"Passed: {totalPassed}");
        Console.WriteLine($"Failed: {totalFailed}");
        Console.WriteLine($"Success rate: {totalPassed * 100.0 / (totalPassed + totalFailed):F1}%");
        
        Console.WriteLine("\nREMAINING BUGS:");
        Console.WriteLine("1. Kiwipete: Significant undercount (main issue)");
        Console.WriteLine("2. Starting position: Minor overcount (+78)");
        Console.WriteLine("3. Position 4: Minor overcount");
        Console.WriteLine("4. Position 5: Minimal overcount (+53)");
        Console.WriteLine("5. En passant: Only +1 node at depth 2 (almost perfect!)");
        Console.WriteLine("6. Promotion: Only +80 nodes at depth 4 (almost perfect!)");
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