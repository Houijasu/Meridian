#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class StartingPositionDepth4Debug
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void FindDepth4Discrepancy()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        Console.WriteLine("Starting position perft(4) analysis:");
        Console.WriteLine("Expected: 197,281");
        Console.WriteLine("Our engine: 197,359 (+78)");
        Console.WriteLine();

        // Stockfish depth 4 results
        var stockfishDepth4 = new Dictionary<string, ulong>
        {
            ["a2a3"] = 8457,
            ["b2b3"] = 9345,
            ["c2c3"] = 9272,
            ["d2d3"] = 11959,
            ["e2e3"] = 13134,
            ["f2f3"] = 8457,
            ["g2g3"] = 9345,
            ["h2h3"] = 8457,
            ["a2a4"] = 9329,
            ["b2b4"] = 9332,
            ["c2c4"] = 9744,
            ["d2d4"] = 12435,
            ["e2e4"] = 13160,
            ["f2f4"] = 8929,
            ["g2g4"] = 9328,
            ["h2h4"] = 9329,
            ["b1a3"] = 8885,
            ["b1c3"] = 9755,
            ["g1f3"] = 9748,
            ["g1h3"] = 8881
        };

        // Calculate our perft divide at depth 4
        var ourResults = new Dictionary<string, ulong>();
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var nodes = Perft(position, 3);
            position.UnmakeMove(move, undoInfo);
            ourResults[move.ToUci()] = nodes;
        }

        // Compare results
        ulong totalDiff = 0;
        var differences = new List<(string move, long diff)>();

        foreach (var (moveStr, stockfishNodes) in stockfishDepth4)
        {
            if (ourResults.TryGetValue(moveStr, out var ourNodes))
            {
                var diff = (long)ourNodes - (long)stockfishNodes;
                if (diff != 0)
                {
                    differences.Add((moveStr, diff));
                    totalDiff = (ulong)Math.Abs(diff) + totalDiff;
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Move {moveStr} not found in our results!");
            }
        }

        // Check for extra moves
        foreach (var (moveStr, nodes) in ourResults)
        {
            if (!stockfishDepth4.ContainsKey(moveStr))
            {
                Console.WriteLine($"ERROR: Extra move generated: {moveStr} ({nodes} nodes)");
                differences.Add((moveStr, (long)nodes));
            }
        }

        Console.WriteLine($"Found {differences.Count} moves with differences:");
        foreach (var (move, diff) in differences.OrderByDescending(d => Math.Abs(d.diff)))
        {
            Console.WriteLine($"  {move}: {diff:+#;-#;0} nodes");
        }

        Console.WriteLine($"\nTotal absolute difference: {totalDiff}");
        
        // Force output by failing the test
        if (differences.Count > 0)
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine("Starting position perft(4) analysis:");
            output.AppendLine("Expected: 197,281");
            output.AppendLine("Our engine: 197,359 (+78)");
            output.AppendLine();
            output.AppendLine($"Found {differences.Count} moves with differences:");
            foreach (var (move, diff) in differences.OrderByDescending(d => Math.Abs(d.diff)))
            {
                output.AppendLine($"  {move}: {diff:+#;-#;0} nodes");
            }
            Assert.Fail(output.ToString());
        }

        // Now let's go deeper into the moves with the largest differences
        if (differences.Count > 0)
        {
            var topDiff = differences.OrderByDescending(d => Math.Abs(d.diff)).First();
            Console.WriteLine($"\nAnalyzing move {topDiff.move} (diff: {topDiff.diff}):");
            AnalyzeMoveDifference(position, topDiff.move);
        }
    }

    private void AnalyzeMoveDifference(Position position, string moveStr)
    {
        // Find the move
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        Move? targetMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == moveStr)
            {
                targetMove = moves[i];
                break;
            }
        }

        if (targetMove == null)
        {
            Console.WriteLine("ERROR: Could not find move!");
            return;
        }

        var move = targetMove.Value;
        var undoInfo = position.MakeMove(move);

        // Perft divide at depth 3 from this position
        Console.WriteLine($"Perft divide at depth 3 after {moveStr}:");
        
        var subMoves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref subMoves);

        var results = new Dictionary<string, ulong>();
        for (int i = 0; i < subMoves.Count; i++)
        {
            var subMove = subMoves[i];
            var subUndoInfo = position.MakeMove(subMove);
            var nodes = Perft(position, 2);
            position.UnmakeMove(subMove, subUndoInfo);
            results[subMove.ToUci()] = nodes;
        }

        // Show top contributors
        foreach (var (subMoveStr, nodes) in results.OrderByDescending(r => r.Value).Take(10))
        {
            Console.WriteLine($"  {subMoveStr}: {nodes}");
        }

        position.UnmakeMove(move, undoInfo);
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