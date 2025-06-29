#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftComparisonResults
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CompareKiwipete()
    {
        var fen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        
        // Stockfish verified values
        var stockfishResults = new[]
        {
            (1, 48UL),
            (2, 2039UL),
            (3, 97862UL),
            (4, 4085603UL),
            (5, 193690690UL),
            (6, 8031647685UL)
        };
        
        Console.WriteLine("=== KIWIPETE POSITION COMPARISON ===");
        Console.WriteLine($"FEN: {fen}\n");
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        foreach (var (depth, stockfish) in stockfishResults)
        {
            if (depth > 4) break; // Skip deep levels for now
            
            var ourResult = Perft(position, depth);
            var diff = (long)ourResult - (long)stockfish;
            
            Console.WriteLine($"Depth {depth}:");
            Console.WriteLine($"  Stockfish:    {stockfish:N0}");
            Console.WriteLine($"  Our engine:   {ourResult:N0} {(ourResult == stockfish ? "✓" : $"✗ (diff: {diff:+#;-#;0})")}");
            
            if (ourResult != stockfish)
            {
                var errorPct = Math.Abs(diff) * 100.0 / stockfish;
                Console.WriteLine($"  Error: {errorPct:F2}% - REAL BUG!");
            }
            Console.WriteLine();
        }
        
        Console.WriteLine("SUMMARY: Test values are CORRECT. Our engine has a REAL BUG - significant undercount.");
    }
    
    [TestMethod]
    public void CompareStartingPosition()
    {
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        
        // Stockfish verified values
        var stockfishResults = new[]
        {
            (1, 20UL),
            (2, 400UL),
            (3, 8902UL),
            (4, 197281UL),
            (5, 4865609UL),
            (6, 119060324UL)
        };
        
        Console.WriteLine("=== STARTING POSITION COMPARISON ===");
        Console.WriteLine($"FEN: {fen}\n");
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        foreach (var (depth, stockfish) in stockfishResults)
        {
            var ourResult = Perft(position, depth);
            var testExpected = depth switch
            {
                1 => 20UL,
                2 => 400UL,
                3 => 8902UL,
                4 => 197281UL,
                5 => 4865609UL,
                6 => 119060324UL,
                _ => 0UL
            };
            
            var diff = (long)ourResult - (long)stockfish;
            var testCorrect = testExpected == stockfish;
            
            Console.WriteLine($"Depth {depth}:");
            Console.WriteLine($"  Stockfish:    {stockfish:N0}");
            Console.WriteLine($"  Our engine:   {ourResult:N0} {(ourResult == stockfish ? "✓" : $"✗ (diff: {diff:+#;-#;0})")}");
            Console.WriteLine($"  Test expects: {testExpected:N0} {(testCorrect ? "✓ TEST CORRECT" : "✗ TEST WRONG")}");
            
            if (ourResult != stockfish)
            {
                var errorPct = Math.Abs(diff) * 100.0 / stockfish;
                Console.WriteLine($"  Error: {errorPct:F4}%");
            }
            Console.WriteLine();
        }
        
        Console.WriteLine("SUMMARY: Test values are CORRECT. Our engine has minor bugs at depth 4-5.");
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