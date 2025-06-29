#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class VerifyCanonicalPerft
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void VerifyEnPassantPosition()
    {
        // The en passant position from the tests
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        
        // CORRECT canonical values from Chess Programming Wiki
        var canonicalValues = new[]
        {
            (1, 31UL),
            (2, 707UL),    // Test expects 908 (WRONG!)
            (3, 21458UL),  // Test expects 27837 (WRONG!)
            (4, 518253UL)  // Test expects 824064 (WRONG!)
        };
        
        Console.WriteLine("=== EN PASSANT POSITION VERIFICATION ===");
        Console.WriteLine($"FEN: {fen}\n");
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        foreach (var (depth, canonical) in canonicalValues)
        {
            var actual = Perft(position, depth);
            var testExpected = depth switch
            {
                1 => 31UL,
                2 => 908UL,    // WRONG test value
                3 => 27837UL,  // WRONG test value
                4 => 824064UL, // WRONG test value
                _ => 0UL
            };
            
            Console.WriteLine($"Depth {depth}:");
            Console.WriteLine($"  Our engine:     {actual:N0}");
            Console.WriteLine($"  Canonical:      {canonical:N0} {(actual == canonical ? "✓" : "✗")}");
            Console.WriteLine($"  Test expects:   {testExpected:N0} {(testExpected == canonical ? "" : "← WRONG!")}");
            
            if (actual != canonical)
            {
                var diff = (long)actual - (long)canonical;
                Console.WriteLine($"  Real error:     {diff:+#;-#;0} ({Math.Abs(diff) * 100.0 / canonical:F2}%)");
            }
            Console.WriteLine();
        }
    }
    
    [TestMethod]
    public void VerifyPosition4()
    {
        // Position 4 from the tests
        var fen = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        
        // Need to verify canonical values
        var testValues = new[]
        {
            (1, 6UL),
            (2, 264UL),
            (3, 9467UL),
            (4, 422333UL)  // Gemini says this should be 403194
        };
        
        Console.WriteLine("=== POSITION 4 VERIFICATION ===");
        Console.WriteLine($"FEN: {fen}\n");
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        foreach (var (depth, testExpected) in testValues)
        {
            var actual = Perft(position, depth);
            
            Console.WriteLine($"Depth {depth}:");
            Console.WriteLine($"  Our engine:     {actual:N0}");
            Console.WriteLine($"  Test expects:   {testExpected:N0}");
            
            if (depth == 4)
            {
                Console.WriteLine($"  Canonical (per Gemini): 403,194");
                var canonicalD4 = 403194UL;
                if (actual == canonicalD4)
                {
                    Console.WriteLine($"  Our engine matches canonical! Test value is WRONG!");
                }
            }
            
            Console.WriteLine();
        }
    }
    
    [TestMethod]
    public void VerifyKiwipete()
    {
        // Kiwipete - this one should have correct test values
        var fen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        
        var canonicalValues = new[]
        {
            (1, 48UL),
            (2, 2039UL),
            (3, 97862UL),
            (4, 4085603UL)
        };
        
        Console.WriteLine("=== KIWIPETE VERIFICATION ===");
        Console.WriteLine($"FEN: {fen}\n");
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("This position should have CORRECT test values.\n");
        
        foreach (var (depth, canonical) in canonicalValues)
        {
            var actual = Perft(position, depth);
            
            Console.WriteLine($"Depth {depth}:");
            Console.WriteLine($"  Our engine:     {actual:N0}");
            Console.WriteLine($"  Canonical:      {canonical:N0}");
            
            if (actual != canonical)
            {
                var diff = (long)actual - (long)canonical;
                Console.WriteLine($"  Real bug:       {diff:+#;-#;0} ({Math.Abs(diff) * 100.0 / canonical:F2}%)");
                Console.WriteLine($"  This is a REAL engine bug that needs fixing!");
            }
            else
            {
                Console.WriteLine($"  ✓ Correct!");
            }
            Console.WriteLine();
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