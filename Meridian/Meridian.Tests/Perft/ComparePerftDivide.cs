#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class ComparePerftDivide
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void CompareWithStockfishPerftDivide()
    {
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Get our perft divide
        var ourResults = GetPerftDivide(position, 2);
        
        // Get Stockfish perft divide  
        var stockfishResults = GetStockfishPerftDivide(fen, 2);
        
        // Compare
        var allMoves = ourResults.Keys.Union(stockfishResults.Keys).OrderBy(x => x);
        
        Console.WriteLine("Move comparison (depth 2):");
        Console.WriteLine("Move\tOurs\tStockfish\tDiff");
        
        var totalDiff = 0L;
        foreach (var move in allMoves)
        {
            var ourCount = ourResults.ContainsKey(move) ? ourResults[move] : 0;
            var sfCount = stockfishResults.ContainsKey(move) ? stockfishResults[move] : 0;
            var diff = (long)ourCount - (long)sfCount;
            
            if (diff != 0)
            {
                Console.WriteLine($"{move}\t{ourCount}\t{sfCount}\t{diff:+#;-#;0}");
            }
            
            totalDiff += diff;
        }
        
        Console.WriteLine($"\nTotal difference: {totalDiff}");
        
        if (totalDiff != 0)
        {
            Assert.Fail($"Perft mismatch: difference of {totalDiff}");
        }
    }
    
    private Dictionary<string, ulong> GetPerftDivide(Position position, int depth)
    {
        var results = new Dictionary<string, ulong>();
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var count = depth > 1 ? Perft(position, depth - 1) : 1;
            position.UnmakeMove(move, undoInfo);
            
            results[move.ToUci()] = count;
        }
        
        return results;
    }
    
    private Dictionary<string, ulong> GetStockfishPerftDivide(string fen, int depth)
    {
        var results = new Dictionary<string, ulong>();
        
        using (var process = new Process())
        {
            process.StartInfo.FileName = "stockfish";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            
            process.Start();
            
            process.StandardInput.WriteLine($"position fen {fen}");
            process.StandardInput.WriteLine($"go perft {depth}");
            process.StandardInput.WriteLine("quit");
            
            string? line;
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                Console.WriteLine($"Stockfish output: {line}");
                
                // Parse lines like "e2e4: 600"
                if (line.Contains(":") && line.Length > 0 && char.IsLetter(line[0]))
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2)
                    {
                        var move = parts[0].Trim();
                        if (ulong.TryParse(parts[1].Trim(), out var count))
                        {
                            results[move] = count;
                        }
                    }
                }
            }
            
            process.WaitForExit();
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