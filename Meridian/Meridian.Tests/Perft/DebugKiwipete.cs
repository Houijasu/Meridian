#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class DebugKiwipete
{
    private readonly MoveGenerator _moveGenerator = new();
    private const string KiwipeteFen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

    [TestMethod]
    public void DebugKiwipeteNodeCount()
    {
        var positionResult = Position.FromFen(KiwipeteFen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        Console.WriteLine($"Testing Kiwipete position: {KiwipeteFen}");
        Console.WriteLine();

        for (int depth = 1; depth <= 5; depth++)
        {
            var sw = Stopwatch.StartNew();
            var nodes = Perft(position, depth);
            sw.Stop();

            var expected = depth switch
            {
                1 => 48UL,
                2 => 2039UL,
                3 => 97862UL,
                4 => 4085603UL,
                5 => 193690690UL,
                _ => 0UL
            };

            var diff = (long)nodes - (long)expected;
            var status = diff == 0 ? "✓" : "✗";
            
            Console.WriteLine($"Depth {depth}: {nodes,12} nodes (expected: {expected,12}) {status} Diff: {diff,8} Time: {sw.ElapsedMilliseconds}ms");
        }
    }

    [TestMethod]
    public void DebugKiwipetePerftDivide()
    {
        var positionResult = Position.FromFen(KiwipeteFen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        Console.WriteLine($"Perft divide for Kiwipete at depth 3:");
        Console.WriteLine();

        var expectedDepth3 = new Dictionary<string, ulong>
        {
            ["a2a3"] = 1796,
            ["b2b3"] = 1930,
            ["c2c3"] = 2103,
            ["d2d3"] = 2522,
            ["d2d4"] = 2308,
            ["e2f1"] = 2322,
            ["e2d1"] = 2045,
            ["e2c4"] = 2278,
            ["e2d3"] = 2522,
            ["e2f3"] = 2116,
            ["e2g4"] = 2217,
            ["f3f4"] = 1959,
            ["f3g3"] = 2136,
            ["f3h3"] = 2010,
            ["f3e3"] = 2393,
            ["f3d3"] = 2256,
            ["f3c3"] = 1494,
            ["f3b3"] = 1357,
            ["f3a3"] = 1009,
            ["f3g4"] = 1943,
            ["f3h5"] = 2182,
            ["f3f6"] = 1623,
            ["f3f7"] = 1910,
            ["a1b1"] = 1969,
            ["a1c1"] = 1866,
            ["a1d1"] = 2137,
            ["h1g1"] = 2066,
            ["h1f1"] = 1969,
            ["e5d3"] = 2080,
            ["e5c4"] = 2031,
            ["e5g4"] = 2070,
            ["e5g6"] = 2069,
            ["e5c6"] = 2269,
            ["e5d7"] = 2240,
            ["e5f7"] = 1981,
            ["c3b1"] = 1795,
            ["c3d1"] = 1836,
            ["c3a4"] = 1990,
            ["c3b5"] = 1975,
            ["d5d6"] = 1762,
            ["d5e6"] = 3041,
            ["e1f1"] = 2318,
            ["e1d1"] = 2392,
            ["e1c1"] = 2213,
            ["e1g1"] = 2598,
            ["e1e2"] = 2039,
            ["h2h3"] = 1970,
            ["h2h4"] = 1894
        };

        var results = PerftDivide(position, 3);
        ulong totalNodes = 0;
        ulong expectedTotal = 0;

        var sortedMoves = new List<string>(results.Keys);
        sortedMoves.Sort();

        foreach (var moveStr in sortedMoves)
        {
            var nodes = results[moveStr];
            totalNodes += nodes;
            
            if (expectedDepth3.TryGetValue(moveStr, out var expected))
            {
                expectedTotal += expected;
                var diff = (long)nodes - (long)expected;
                var status = diff == 0 ? "✓" : "✗";
                Console.WriteLine($"{moveStr}: {nodes,6} (expected: {expected,6}) {status} Diff: {diff,6}");
            }
            else
            {
                Console.WriteLine($"{moveStr}: {nodes,6} (NOT IN EXPECTED LIST) ✗");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total nodes: {totalNodes} (expected: {expectedTotal})");
        Console.WriteLine($"Total diff: {(long)totalNodes - (long)expectedTotal}");

        foreach (var expectedMove in expectedDepth3.Keys)
        {
            if (!results.ContainsKey(expectedMove))
            {
                Console.WriteLine($"Missing move: {expectedMove} (expected {expectedDepth3[expectedMove]} nodes)");
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

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(move, undoInfo);
        }

        return nodes;
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
}