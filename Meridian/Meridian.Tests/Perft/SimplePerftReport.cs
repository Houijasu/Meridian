#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;

namespace Meridian.Tests.Perft;

[TestClass]
public class SimplePerftReport
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void ShowFailedPerftTests()
    {
        var results = new List<string>();
        
        // Test starting position
        TestPosition("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 
                    new[] { (1, 20UL), (2, 400UL), (3, 8902UL), (4, 197281UL) }, 
                    "Starting position", results);
        
        // Test Kiwipete
        TestPosition("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                    new[] { (1, 48UL), (2, 2039UL), (3, 97862UL), (4, 4085603UL) },
                    "Kiwipete", results);
        
        // Test Position 3
        TestPosition("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                    new[] { (1, 14UL), (2, 191UL), (3, 2812UL), (4, 43238UL) },
                    "Position 3", results);
        
        // Test Castling
        TestPosition("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1",
                    new[] { (1, 26UL), (2, 568UL), (3, 13744UL), (4, 314346UL) },
                    "Castling", results);
        
        // Test Promotion
        TestPosition("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1",
                    new[] { (1, 18UL), (2, 270UL), (3, 4699UL), (4, 73683UL) },
                    "Promotion", results);
        
        // Test En passant
        TestPosition("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3",
                    new[] { (1, 31UL), (2, 707UL), (3, 27837UL), (4, 824064UL) },
                    "En passant", results);
        
        foreach (var result in results)
        {
            Console.WriteLine(result);
        }
        
        if (results.Count > 0)
        {
            Assert.Fail($"Found {results.Count} perft test failures");
        }
    }
    
    private void TestPosition(string fen, (int depth, ulong expected)[] tests, string name, List<string> results)
    {
        var positionResult = Position.FromFen(fen);
        if (!positionResult.IsSuccess) return;
        
        foreach (var (depth, expected) in tests)
        {
            var actual = Perft(positionResult.Value, depth);
            if (actual != expected)
            {
                results.Add($"{name} - Depth {depth}:");
                results.Add($"  FEN: {fen}");
                results.Add($"  Expected: {expected}");
                results.Add($"  Actual: {actual}");
                results.Add($"  Difference: {(long)actual - (long)expected}");
                results.Add("");
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
}