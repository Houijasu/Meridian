#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftDivideComparison
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void CompareStartingPositionDepth3()
    {
        // Starting position depth 4 has +30 nodes, so let's check depth 3 divide
        var fenResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        var expectedMoves = new Dictionary<string, ulong>
        {
            ["a2a3"] = 380UL,
            ["a2a4"] = 420UL,
            ["b2b3"] = 420UL,
            ["b2b4"] = 421UL,
            ["c2c3"] = 420UL,
            ["c2c4"] = 441UL,
            ["d2d3"] = 539UL,
            ["d2d4"] = 560UL,
            ["e2e3"] = 599UL,
            ["e2e4"] = 600UL,
            ["f2f3"] = 380UL,
            ["f2f4"] = 401UL,
            ["g2g3"] = 420UL,
            ["g2g4"] = 421UL,
            ["h2h3"] = 380UL,
            ["h2h4"] = 420UL,
            ["b1a3"] = 400UL,
            ["b1c3"] = 440UL,
            ["g1f3"] = 440UL,
            ["g1h3"] = 400UL
        };

        var results = PerftDivide(position, 3);
        
        Console.WriteLine("Move comparison for starting position depth 3:");
        foreach (var (moveStr, expected) in expectedMoves.OrderBy(x => x.Key))
        {
            if (results.TryGetValue(moveStr, out var actual))
            {
                if (actual != expected)
                {
                    Console.WriteLine($"{moveStr}: expected {expected}, got {actual} (diff: {(long)actual - (long)expected:+#;-#;0})");
                }
            }
            else
            {
                Console.WriteLine($"{moveStr}: MISSING MOVE");
            }
        }
        
        // Check for extra moves we generate that Stockfish doesn't
        foreach (var (moveStr, count) in results.OrderBy(x => x.Key))
        {
            if (!expectedMoves.ContainsKey(moveStr))
            {
                Console.WriteLine($"{moveStr}: EXTRA MOVE with {count} nodes");
            }
        }
    }

    [TestMethod]
    public void CompareEnPassantPositionDepth1()
    {
        // En passant position depth 2 has -5 nodes, so let's check depth 1 divide
        var fenResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        
        // Get perft divide at depth 1
        var results = PerftDivide(position, 1);
        
        Console.WriteLine("\nAll moves from en passant position:");
        foreach (var (moveStr, count) in results.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{moveStr}: {count}");
        }
        Console.WriteLine($"Total moves: {results.Count}");
    }

    [TestMethod]
    public void CompareKiwipeteDepth1()
    {
        // Kiwipete depth 2 has -42 nodes
        var fenResult = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        var results = PerftDivide(position, 1);
        
        Console.WriteLine("\nAll moves from Kiwipete position:");
        foreach (var (moveStr, count) in results.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{moveStr}: {count}");
        }
        Console.WriteLine($"Total moves: {results.Count} (expected 48)");
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