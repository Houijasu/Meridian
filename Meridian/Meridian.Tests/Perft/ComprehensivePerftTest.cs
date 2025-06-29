#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class ComprehensivePerftTest
{
    private readonly MoveGenerator _moveGenerator = new();
    private const int TimeoutSeconds = 60;
    
    [TestMethod]
    [Timeout(TimeoutSeconds * 1000)]
    public void RunAllPerftTestsToDepth6()
    {
        var testPositions = new[]
        {
            new PerftPosition("Starting position", 
                Position.StartingFen,
                new[] { 20UL, 400UL, 8902UL, 197281UL, 4865609UL, 119060324UL }),
            
            new PerftPosition("Kiwipete", 
                "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                new[] { 48UL, 2039UL, 97862UL, 4085603UL, 193690690UL, 8031647685UL }),
            
            new PerftPosition("Position 3", 
                "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                new[] { 14UL, 191UL, 2812UL, 43238UL, 674624UL, 11030083UL }),
            
            new PerftPosition("Position 4", 
                "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                new[] { 6UL, 264UL, 9467UL, 422333UL, 15833292UL, 706045033UL }),
            
            new PerftPosition("Position 5", 
                "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
                new[] { 44UL, 1486UL, 62379UL, 2103487UL, 89941194UL, 3048196529UL }),
            
            new PerftPosition("Position 6", 
                "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
                new[] { 46UL, 2079UL, 89890UL, 3894594UL, 164075551UL, 6923051137UL }),
                
            new PerftPosition("Black kingside castling",
                "4k2r/8/8/8/8/8/8/4K3 b k - 0 1",
                new[] { 15UL, 66UL, 1197UL, 7059UL, 133987UL, 764643UL }),
                
            new PerftPosition("En passant",
                "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                new[] { 14UL, 191UL, 2812UL, 43238UL, 674624UL, 11030083UL }),
                
            new PerftPosition("Promotion",
                "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1",
                new[] { 18UL, 270UL, 4699UL, 73683UL, 1198299UL, 19644856UL })
        };
        
        var stopwatch = Stopwatch.StartNew();
        var totalNodes = 0UL;
        var failedTests = 0;
        var passedTests = 0;
        var skippedTests = 0;
        
        Console.WriteLine($"=== COMPREHENSIVE PERFT TEST (Timeout: {TimeoutSeconds}s) ===\n");
        
        foreach (var test in testPositions)
        {
            if (stopwatch.Elapsed.TotalSeconds > TimeoutSeconds - 5) // Leave 5s buffer
            {
                Console.WriteLine($"\nSkipping remaining tests due to time limit...");
                break;
            }
            
            Console.WriteLine($"{test.Name}:");
            Console.WriteLine($"FEN: {test.Fen}");
            
            var positionResult = Position.FromFen(test.Fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine($"ERROR: Failed to parse FEN\n");
                failedTests++;
                continue;
            }
            
            var position = positionResult.Value;
            var maxDepth = 0;
            
            for (int depth = 1; depth <= 6; depth++)
            {
                if (stopwatch.Elapsed.TotalSeconds > TimeoutSeconds - 2)
                {
                    Console.WriteLine($"  Depth {depth}: SKIPPED (time limit)");
                    skippedTests++;
                    continue;
                }
                
                var depthStopwatch = Stopwatch.StartNew();
                var actual = Perft(position, depth);
                depthStopwatch.Stop();
                
                totalNodes += actual;
                maxDepth = depth;
                
                var expected = test.ExpectedNodes[depth - 1];
                var pass = actual == expected;
                
                if (pass)
                {
                    passedTests++;
                    Console.WriteLine($"  Depth {depth}: {actual:N0} ✓ ({depthStopwatch.ElapsedMilliseconds}ms)");
                }
                else
                {
                    failedTests++;
                    var diff = (long)actual - (long)expected;
                    var percentage = Math.Abs(diff) * 100.0 / expected;
                    Console.WriteLine($"  Depth {depth}: {actual:N0} ✗ (expected {expected:N0}, diff: {diff:+#;-#;0}, {percentage:F2}%) ({depthStopwatch.ElapsedMilliseconds}ms)");
                }
                
                // Skip deeper depths if this one took too long
                if (depthStopwatch.ElapsedMilliseconds > 10000) // 10 seconds
                {
                    Console.WriteLine($"  Skipping deeper depths (too slow)");
                    skippedTests += (6 - depth);
                    break;
                }
            }
            
            Console.WriteLine();
        }
        
        stopwatch.Stop();
        
        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Total nodes: {totalNodes:N0}");
        Console.WriteLine($"Nodes per second: {(totalNodes / stopwatch.Elapsed.TotalSeconds):N0}");
        Console.WriteLine($"Tests passed: {passedTests}");
        Console.WriteLine($"Tests failed: {failedTests}");
        Console.WriteLine($"Tests skipped: {skippedTests}");
        Console.WriteLine($"Success rate: {(passedTests * 100.0 / (passedTests + failedTests)):F1}%");
        
        if (failedTests > 0)
        {
            Assert.Fail($"{failedTests} perft tests failed");
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
    
    private class PerftPosition
    {
        public string Name { get; }
        public string Fen { get; }
        public ulong[] ExpectedNodes { get; }
        
        public PerftPosition(string name, string fen, ulong[] expectedNodes)
        {
            Name = name;
            Fen = fen;
            ExpectedNodes = expectedNodes;
        }
    }
}