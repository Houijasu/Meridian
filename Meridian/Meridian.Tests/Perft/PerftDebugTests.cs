#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftDebugTests
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugPerftDivide_Depth4()
    {
        var fenResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        var results = PerftDivide(position, 4);
        
        // Expected values from Stockfish
        var expected = new Dictionary<string, ulong>
        {
            ["a2a3"] = 8457UL,
            ["a2a4"] = 9329UL,
            ["b2b3"] = 9345UL,
            ["b2b4"] = 9332UL,
            ["c2c3"] = 9272UL,
            ["c2c4"] = 9744UL,
            ["d2d3"] = 11959UL,
            ["d2d4"] = 12435UL,
            ["e2e3"] = 13134UL,
            ["e2e4"] = 13160UL,
            ["f2f3"] = 8457UL,
            ["f2f4"] = 8929UL,
            ["g2g3"] = 9345UL,
            ["g2g4"] = 9328UL,
            ["h2h3"] = 8457UL,
            ["h2h4"] = 9329UL,
            ["b1a3"] = 8885UL,
            ["b1c3"] = 9755UL,
            ["g1f3"] = 9748UL,
            ["g1h3"] = 8881UL
        };
        
        var totalExpected = 0UL;
        var totalActual = 0UL;
        
        var diffs = new List<string>();
        
        foreach (var (move, expectedCount) in expected)
        {
            totalExpected += expectedCount;
            if (results.TryGetValue(move, out var actualCount))
            {
                totalActual += actualCount;
                if (expectedCount != actualCount)
                {
                    var diff = $"Move {move}: Expected {expectedCount}, Got {actualCount} (diff: {(long)actualCount - (long)expectedCount})";
                    Console.WriteLine(diff);
                    diffs.Add(diff);
                }
            }
            else
            {
                var msg = $"Move {move}: Missing from results!";
                Console.WriteLine(msg);
                diffs.Add(msg);
            }
        }
        
        foreach (var (move, count) in results)
        {
            if (!expected.ContainsKey(move))
            {
                Console.WriteLine($"Unexpected move {move} with count {count}");
                totalActual += count;
            }
        }
        
        Console.WriteLine($"\nTotal: Expected {totalExpected}, Got {totalActual} (diff: {(long)totalActual - (long)totalExpected})");
        
        if (diffs.Count > 0)
        {
            Assert.Fail($"Perft differences found:\n{string.Join("\n", diffs)}");
        }
    }
    
    private Dictionary<string, ulong> PerftDivide(Position position, int depth)
    {
        var results = new Dictionary<string, ulong>();
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var nodes = depth > 1 ? Perft(position, depth - 1) : 1;
            position.UnmakeMove(move, undoInfo);
            results[move.ToUci()] = nodes;
        }

        return results;
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