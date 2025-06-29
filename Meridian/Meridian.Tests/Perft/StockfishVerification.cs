#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Diagnostics;
using System.IO;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class StockfishVerification
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void GenerateStockfishVerificationCommands()
    {
        var testPositions = new[]
        {
            ("Starting position", 
                "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
                new[] { 20UL, 400UL, 8902UL, 197281UL, 4865609UL, 119060324UL }),
            
            ("Kiwipete", 
                "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                new[] { 48UL, 2039UL, 97862UL, 4085603UL, 193690690UL, 8031647685UL }),
            
            ("Position 3", 
                "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                new[] { 14UL, 191UL, 2812UL, 43238UL, 674624UL, 11030083UL }),
            
            ("Position 4", 
                "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
                new[] { 6UL, 264UL, 9467UL, 422333UL, 15833292UL, 706045033UL }),
            
            ("Position 5 (CPW)", 
                "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
                new[] { 44UL, 1486UL, 62379UL, 2103487UL, 89941194UL, 3048196529UL }),
            
            ("Black kingside castling",
                "4k2r/8/8/8/8/8/8/4K3 b k - 0 1",
                new[] { 15UL, 66UL, 1197UL, 7059UL, 133987UL, 764643UL }),
                
            ("En passant position",
                "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3",
                new[] { 31UL, 0UL, 0UL, 0UL, 0UL, 0UL }), // Need to verify with Stockfish
                
            ("Promotion position",
                "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1",
                new[] { 18UL, 0UL, 0UL, 0UL, 0UL, 0UL }), // Need to verify with Stockfish
                
            ("Position 6",
                "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
                new[] { 46UL, 0UL, 0UL, 0UL, 0UL, 0UL }) // Need to verify with Stockfish
        };
        
        Span<Move> moveBuffer = stackalloc Move[218];
        
        foreach (var (name, fen, testExpected) in testPositions)
        {
            // Create a file to write the output to
            using var writer = new StreamWriter("stockfish_verification.txt", true);
            
            writer.WriteLine($"\n{new string('=', 80)}");
            writer.WriteLine($"{name}");
            writer.WriteLine($"FEN: {fen}");
            writer.WriteLine($"{new string('=', 80)}\n");
            
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                writer.WriteLine($"ERROR: Failed to parse FEN");
                continue;
            }
            
            var position = positionResult.Value;
            
            // Generate our results
            writer.WriteLine("Our engine results:");
            var ourResults = new ulong[6];
            for (int depth = 1; depth <= 6; depth++)
            {
                var sw = Stopwatch.StartNew();
                if (sw.Elapsed.TotalSeconds > 30)
                {
                    writer.WriteLine($"Depth {depth}: SKIPPED (too slow)");
                    break;
                }
                
                var nodes = Perft(position, depth);
                sw.Stop();
                ourResults[depth - 1] = nodes;
                
                var testExp = testExpected[depth - 1];
                if (testExp > 0)
                {
                    var match = nodes == testExp;
                    writer.WriteLine($"Depth {depth}: {nodes:N0} {(match ? "✓" : $"✗ (test expects {testExp:N0})")} ({sw.ElapsedMilliseconds}ms)");
                }
                else
                {
                    writer.WriteLine($"Depth {depth}: {nodes:N0} (no test data) ({sw.ElapsedMilliseconds}ms)");
                }
                
                if (sw.ElapsedMilliseconds > 5000)
                {
                    writer.WriteLine("Skipping deeper depths (too slow)");
                    break;
                }
            }
            
            // Generate Stockfish commands
            writer.WriteLine($"\nStockfish verification commands:");
            writer.WriteLine("```");
            writer.WriteLine($"position fen {fen}");
            for (int depth = 1; depth <= 6; depth++)
            {
                writer.WriteLine($"go perft {depth}");
            }
            writer.WriteLine("```");
            
            // Show perft divide for depth 1
            writer.WriteLine($"\nPerft divide at depth 1:");
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                var undoInfo = position.MakeMove(move);
                var nodes = Perft(position, 0);
                position.UnmakeMove(move, undoInfo);
                writer.WriteLine($"{move.ToUci()}: {nodes}");
            }
            writer.WriteLine($"Total: {moves.Count}");
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