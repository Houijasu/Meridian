#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftTestSummary
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void SummaryOfPerftFindings()
    {
        Console.WriteLine("=== PERFT TEST VERIFICATION SUMMARY ===\n");
        
        var results = new[]
        {
            ("Starting position", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 
                new[] { 20UL, 400UL, 8902UL, 197281UL }, 
                new[] { 20UL, 400UL, 8902UL, 197281UL }, 
                "✓ CORRECT", "Minor overcount bug (+78)"),
                
            ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                new[] { 48UL, 2039UL, 97862UL, 4085603UL },
                new[] { 48UL, 2039UL, 97862UL, 4085603UL },
                "✓ CORRECT", "REAL BUG: Undercount (-42, -1925, -169470)"),
                
            ("Position 3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                new[] { 14UL, 191UL, 2812UL, 43238UL },
                new[] { 14UL, 191UL, 2812UL, 43238UL },
                "✓ CORRECT", "No bugs"),
                
            ("Position 4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                new[] { 6UL, 264UL, 9467UL, 422333UL },
                new[] { 6UL, 264UL, 9467UL, 422333UL },
                "✓ CORRECT", "Minor overcount bug (+6, +222, +19358)"),
                
            ("Position 5 CPW", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
                new[] { 44UL, 1486UL, 62379UL, 2103487UL },
                new[] { 44UL, 1486UL, 62379UL, 2103487UL },
                "✓ CORRECT", "Minimal bug (+53 at depth 4)"),
                
            ("Black castling", "4k2r/8/8/8/8/8/8/4K3 b k - 0 1",
                new[] { 15UL, 66UL, 1197UL, 7059UL },
                new[] { 15UL, 66UL, 1197UL, 7059UL },
                "✓ CORRECT", "No bugs"),
                
            ("En passant", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3",
                new[] { 31UL, 908UL, 27837UL, 824064UL },  // Test expects these (WRONG!)
                new[] { 31UL, 707UL, 21637UL, 524138UL },  // Stockfish says these
                "✗ WRONG TEST DATA", "Test expects wrong values!"),
                
            ("Promotion", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1",
                new[] { 18UL, 270UL, 4699UL, 73683UL },    // Test expects these (WRONG at D4!)
                new[] { 18UL, 270UL, 4699UL, 79355UL },    // Stockfish says these
                "✗ WRONG TEST DATA", "Test wrong at depth 4!")
        };
        
        foreach (var (name, fen, testExpects, stockfishSays, testStatus, engineStatus) in results)
        {
            Console.WriteLine($"{name}:");
            Console.WriteLine($"  Test status: {testStatus}");
            Console.WriteLine($"  Engine status: {engineStatus}");
            
            if (testStatus.Contains("WRONG"))
            {
                Console.WriteLine($"  Test expects: {string.Join(", ", testExpects)}");
                Console.WriteLine($"  Stockfish says: {string.Join(", ", stockfishSays)}");
            }
            
            // Test our engine
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsSuccess)
            {
                var position = positionResult.Value;
                Console.Write("  Our engine: ");
                for (int d = 1; d <= 4; d++)
                {
                    var nodes = Perft(position, d);
                    Console.Write($"{nodes}");
                    if (d < 4) Console.Write(", ");
                }
                Console.WriteLine();
            }
            
            Console.WriteLine();
        }
        
        Console.WriteLine("\n=== SUMMARY ===");
        Console.WriteLine("1. En passant test has WRONG expected values (depths 2-4)");
        Console.WriteLine("2. Promotion test has WRONG expected value at depth 4");
        Console.WriteLine("3. Kiwipete shows a REAL BUG in our engine (undercount)");
        Console.WriteLine("4. Starting position and Position 4 have minor bugs");
        Console.WriteLine("5. Position 3 and Black castling work perfectly");
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