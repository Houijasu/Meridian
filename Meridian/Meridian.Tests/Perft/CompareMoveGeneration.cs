#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class CompareMoveGeneration
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void CompareSpecificPositions()
    {
        // These are positions where we suspect issues
        var testPositions = new[]
        {
            // After 1.h4
            ("rnbqkbnr/pppppppp/8/8/7P/8/PPPPPPP1/RNBQKBNR b KQkq - 0 1", 20),
            // After 1.b4
            ("rnbqkbnr/pppppppp/8/8/1P6/8/P1PPPPPP/RNBQKBNR b KQkq - 0 1", 20),
            // After 1.g4
            ("rnbqkbnr/pppppppp/8/8/6P1/8/PPPPPP1P/RNBQKBNR b KQkq - 0 1", 20),
        };
        
        Span<Move> moveBuffer = stackalloc Move[218];
        
        foreach (var (fen, expectedMoves) in testPositions)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            var position = positionResult.Value;
            
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            Console.WriteLine($"\nFEN: {fen}");
            Console.WriteLine($"Expected moves: {expectedMoves}");
            Console.WriteLine($"Generated moves: {moves.Count}");
            
            if (moves.Count != expectedMoves)
            {
                Console.WriteLine("Move list:");
                var moveStrings = new List<string>();
                for (int i = 0; i < moves.Count; i++)
                {
                    moveStrings.Add(moves[i].ToUci());
                }
                
                foreach (var move in moveStrings.OrderBy(m => m))
                {
                    Console.WriteLine($"  {move}");
                }
            }
            
            Assert.AreEqual(expectedMoves, moves.Count, $"Move count mismatch for {fen}");
        }
    }
    
    [TestMethod]  
    public void AnalyzeDepth1Differences()
    {
        // Starting position
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        // Expected perft(1) for each move from Stockfish
        var stockfishPerft1 = new Dictionary<string, int>
        {
            ["a2a3"] = 20, ["b2b3"] = 20, ["c2c3"] = 20, ["d2d3"] = 20,
            ["e2e3"] = 20, ["f2f3"] = 20, ["g2g3"] = 20, ["h2h3"] = 20,
            ["a2a4"] = 20, ["b2b4"] = 20, ["c2c4"] = 20, ["d2d4"] = 20,
            ["e2e4"] = 20, ["f2f4"] = 20, ["g2g4"] = 20, ["h2h4"] = 20,
            ["b1a3"] = 20, ["b1c3"] = 20, ["g1f3"] = 20, ["g1h3"] = 20
        };
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine("Checking perft(1) for all starting moves:");
        bool foundDifference = false;
        
        Span<Move> responseBuffer = stackalloc Move[218];
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var moveUci = move.ToUci();
            
            // Make move
            var undoInfo = position.MakeMove(move);
            
            // Count responses
            var responses = new MoveList(responseBuffer);
            _moveGenerator.GenerateMoves(position, ref responses);
            
            var expectedCount = stockfishPerft1.GetValueOrDefault(moveUci, -1);
            if (expectedCount != -1 && responses.Count != expectedCount)
            {
                Console.WriteLine($"\n{moveUci}: Expected {expectedCount}, Got {responses.Count} (diff: {responses.Count - expectedCount})");
                foundDifference = true;
                
                // Show the responses
                var responseList = new List<string>();
                for (int j = 0; j < responses.Count; j++)
                {
                    responseList.Add(responses[j].ToUci());
                }
                
                Console.WriteLine("Responses:");
                foreach (var resp in responseList.OrderBy(r => r))
                {
                    Console.WriteLine($"  {resp}");
                }
            }
            
            // Unmake move
            position.UnmakeMove(move, undoInfo);
        }
        
        if (!foundDifference)
        {
            Console.WriteLine("\nAll perft(1) values match Stockfish!");
        }
        else
        {
            Assert.Fail("Found differences in perft(1) values");
        }
    }
}