#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class PerftFailingSummary
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void ShowAllFailingPositions()
    {
        var testPositions = new[]
        {
            ("Starting position", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 
                new ulong[] { 20, 400, 8902, 197281, 4865609 }),
            
            ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                new ulong[] { 48, 2039, 97862, 4085603 }),
            
            ("Position 3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                new ulong[] { 14, 191, 2812, 43238 }),
            
            ("Position 4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                new ulong[] { 6, 264, 9467, 422333 }),
            
            ("Position 5", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
                new ulong[] { 44, 1486, 62379, 2103487 }),
            
            ("Position 6", "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
                new ulong[] { 46, 2079, 89890, 3894594 }),
                
            ("Promotion", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1",
                new ulong[] { 18, 270, 4699, 79355 }),
                
            ("En Passant", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3",
                new ulong[] { 31, 707, 21637, 524138 }),
                
            ("Black Castling", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R b KQkq - 0 1",
                new ulong[] { 48, 2039, 97862, 4085603 })
        };

        var failingPositions = new List<string>();
        var sb = new StringBuilder();
        
        sb.AppendLine("PERFT TEST RESULTS");
        sb.AppendLine("==================");

        foreach (var (name, fen, expected) in testPositions)
        {
            sb.AppendLine($"\n{name}:");
            sb.AppendLine($"FEN: {fen}");
            
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                sb.AppendLine("  ERROR: Failed to parse FEN");
                failingPositions.Add($"{name}: Failed to parse FEN");
                continue;
            }
            
            var position = positionResult.Value;
            bool hasFailed = false;

            for (int depth = 1; depth <= Math.Min(4, expected.Length); depth++)
            {
                if (expected[depth - 1] == 0) continue;
                
                var nodes = Perft(position, depth);
                var expectedNodes = expected[depth - 1];
                var diff = (long)nodes - (long)expectedNodes;
                var passed = nodes == expectedNodes;
                
                if (!passed && !hasFailed)
                {
                    hasFailed = true;
                    failingPositions.Add($"{name} at depth {depth}: {nodes} (expected {expectedNodes}, diff: {diff:+#;-#;0})");
                }
                
                var status = passed ? "✓" : "✗";
                var diffStr = diff == 0 ? "" : $" (diff: {diff:+#;-#;0})";
                
                sb.AppendLine($"  Depth {depth}: {nodes,10} {status} (expected: {expectedNodes}){diffStr}");
            }
        }

        sb.AppendLine("\n\nFAILING POSITIONS SUMMARY:");
        sb.AppendLine("==========================");
        foreach (var failure in failingPositions)
        {
            sb.AppendLine($"- {failure}");
        }

        // Force output by failing the test with the summary
        if (failingPositions.Count > 0)
        {
            Assert.Fail(sb.ToString());
        }
        else
        {
            // If all pass, still show the results
            Console.WriteLine(sb.ToString());
        }
    }

    private ulong Perft(Position position, int depth)
    {
        if (depth == 0) return 1;

        ulong nodes = 0;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(move, undoInfo);
        }

        return nodes;
    }
}