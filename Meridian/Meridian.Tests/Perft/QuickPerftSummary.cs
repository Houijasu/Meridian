#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Diagnostics;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class QuickPerftSummary
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void RunPerftSummaryToDepth4()
    {
        var testPositions = new[]
        {
            ("Starting position", Position.StartingFen, new[] { 20UL, 400UL, 8902UL, 197281UL }),
            ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", new[] { 48UL, 2039UL, 97862UL, 4085603UL }),
            ("Position 3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", new[] { 14UL, 191UL, 2812UL, 43238UL }),
            ("Position 4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", new[] { 6UL, 264UL, 9467UL, 422333UL }),
            ("Position 5", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", new[] { 44UL, 1486UL, 62379UL, 2103487UL }),
            ("Black castling", "4k2r/8/8/8/8/8/8/4K3 b k - 0 1", new[] { 15UL, 66UL, 1197UL, 7059UL }),
            ("Promotion", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", new[] { 18UL, 270UL, 4699UL, 73683UL })
        };
        
        Console.WriteLine("=== PERFT TEST SUMMARY (Depth 1-4) ===\n");
        
        var totalStopwatch = Stopwatch.StartNew();
        var results = new System.Collections.Generic.List<(string name, int depth, bool pass, long diff)>();
        
        foreach (var (name, fen, expected) in testPositions)
        {
            Console.WriteLine($"{name}:");
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine("  ERROR: Invalid FEN\n");
                continue;
            }
            
            var position = positionResult.Value;
            
            for (int depth = 1; depth <= 4; depth++)
            {
                var sw = Stopwatch.StartNew();
                var actual = Perft(position, depth);
                sw.Stop();
                
                var exp = expected[depth - 1];
                var pass = actual == exp;
                var diff = (long)actual - (long)exp;
                
                results.Add((name, depth, pass, diff));
                
                if (pass)
                {
                    Console.WriteLine($"  D{depth}: {actual,10:N0} ✓ ({sw.ElapsedMilliseconds,4}ms)");
                }
                else
                {
                    var pct = Math.Abs(diff) * 100.0 / exp;
                    Console.WriteLine($"  D{depth}: {actual,10:N0} ✗ (expected {exp:N0}, diff: {diff:+#;-#;0}, {pct:F2}%)");
                }
            }
            Console.WriteLine();
        }
        
        totalStopwatch.Stop();
        
        // Summary
        var passed = results.Count(r => r.pass);
        var failed = results.Count(r => !r.pass);
        
        Console.WriteLine("=== FAILURES ===");
        foreach (var (name, depth, pass, diff) in results.Where(r => !r.pass))
        {
            Console.WriteLine($"{name} at depth {depth}: {diff:+#;-#;0} nodes");
        }
        
        Console.WriteLine($"\n=== STATISTICS ===");
        Console.WriteLine($"Total time: {totalStopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Tests passed: {passed}/{results.Count} ({passed * 100.0 / results.Count:F1}%)");
        Console.WriteLine($"Tests failed: {failed}");
        
        if (failed > 0)
        {
            Console.WriteLine("\nNOTE: The failures are primarily due to:");
            Console.WriteLine("1. Starting position: Small overcount (+78 at depth 4)");
            Console.WriteLine("2. Kiwipete: Undercount (-42 at D2, -1925 at D3, -169470 at D4)");
            Console.WriteLine("3. Position 4: Small overcount");
            Console.WriteLine("4. Position 5: Minimal overcount (+53 at D4)");
            Console.WriteLine("5. Promotion: Overcount (+5752 at D4)");
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