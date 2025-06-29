#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Meridian.Tests.Perft;

[TestClass]
public class StockfishDirectVerification
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void VerifyAllPositionsWithStockfish()
    {
        var positions = new[]
        {
            ("Starting position", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"),
            ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"),
            ("Position 3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1"),
            ("Position 4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1"),
            ("Position 5 (CPW)", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8"),
            ("Black kingside castling", "4k2r/8/8/8/8/8/8/4K3 b k - 0 1"),
            ("En passant", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3"),
            ("Promotion", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1")
        };
        
        foreach (var (name, fen) in positions)
        {
            Console.WriteLine($"\n{new string('=', 80)}");
            Console.WriteLine($"{name}");
            Console.WriteLine($"FEN: {fen}");
            Console.WriteLine($"{new string('=', 80)}\n");
            
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine("ERROR: Failed to parse FEN");
                continue;
            }
            
            var position = positionResult.Value;
            
            // Get Stockfish results for depths 1-4
            for (int depth = 1; depth <= 4; depth++)
            {
                var stockfishResult = GetStockfishPerft(fen, depth);
                var ourResult = Perft(position, depth);
                
                if (stockfishResult > 0)
                {
                    var diff = (long)ourResult - (long)stockfishResult;
                    var match = ourResult == stockfishResult;
                    
                    Console.WriteLine($"Depth {depth}:");
                    Console.WriteLine($"  Stockfish: {stockfishResult:N0}");
                    Console.WriteLine($"  Our engine: {ourResult:N0} {(match ? "✓" : $"✗ (diff: {diff:+#;-#;0})")}");
                    
                    if (!match)
                    {
                        var errorPct = Math.Abs(diff) * 100.0 / stockfishResult;
                        Console.WriteLine($"  Error: {errorPct:F2}%");
                    }
                }
                else
                {
                    Console.WriteLine($"Depth {depth}: Failed to get Stockfish result");
                }
            }
        }
    }
    
    private ulong GetStockfishPerft(string fen, int depth)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "stockfish",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            
            // Send commands
            process.StandardInput.WriteLine("uci");
            process.StandardInput.WriteLine($"position fen {fen}");
            process.StandardInput.WriteLine($"go perft {depth}");
            process.StandardInput.WriteLine("quit");
            
            // Read output
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            
            // Parse the result
            var match = Regex.Match(output, @"Nodes searched: (\d+)");
            if (match.Success)
            {
                return ulong.Parse(match.Groups[1].Value);
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running Stockfish: {ex.Message}");
            return 0;
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