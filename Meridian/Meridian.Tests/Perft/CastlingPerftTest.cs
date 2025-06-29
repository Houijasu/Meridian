#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class CastlingPerftTest
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestBlackKingsideCastlingPosition()
    {
        // Position: 4k2r/8/8/8/8/8/8/4K3 b k - 0 1
        // Black king on e8, black rook on h8, white king on e1
        // Black has kingside castling rights
        var fen = "4k2r/8/8/8/8/8/8/4K3 b k - 0 1";
        var expectedResults = new (int depth, ulong expected)[]
        {
            (1, 15UL),
            (2, 66UL),
            (3, 1197UL),
            (4, 7059UL),
            (5, 133987UL),
            (6, 764643UL)
        };
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Testing position: {fen}");
        Console.WriteLine($"Black to move with kingside castling rights");
        Console.WriteLine();
        
        foreach (var (depth, expected) in expectedResults)
        {
            var actual = Perft(position, depth);
            var diff = (long)actual - (long)expected;
            var pass = actual == expected;
            
            Console.WriteLine($"Depth {depth}: {actual:N0} (expected {expected:N0}) {(pass ? "✓" : $"✗ diff: {diff:+#;-#;0}")}");
            
            Assert.AreEqual(expected, actual, $"Perft failed at depth {depth}. Expected {expected}, got {actual}");
        }
    }
    
    [TestMethod]
    public void TestDepth1Moves()
    {
        var fen = "4k2r/8/8/8/8/8/8/4K3 b k - 0 1";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Position: {fen}");
        Console.WriteLine($"Generated {moves.Count} moves (expected 15):");
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            Console.WriteLine($"{i+1}. {move.ToUci()} {(move.IsCastling ? "(castling)" : "")}");
        }
        
        Assert.AreEqual(15, moves.Count, "Should generate exactly 15 moves");
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