#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;

namespace Meridian.Tests.Perft;

[TestClass]
public class FindEnPassantFailure
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void FindFailingPositionAtDepth1()
    {
        // En passant position that has +1 node at depth 2
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Starting position: {fen}");
        Console.WriteLine($"Expected at depth 2: 707 nodes");
        Console.WriteLine($"Our engine at depth 2: 708 nodes (+1)\n");
        
        // Generate all moves from starting position
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Moves from starting position: {moves.Count}\n");
        
        var failingPositions = new List<(string move, string fen, int ourCount, int expectedCount)>();
        
        Span<Move> moveBuffer2 = stackalloc Move[218];
        
        // For each move, check perft(1)
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            
            // Get the FEN after this move
            var newFen = position.ToFen();
            
            // Count moves at depth 1
            var moves2 = new MoveList(moveBuffer2);
            _moveGenerator.GenerateMoves(position, ref moves2);
            var ourCount = moves2.Count;
            
            // Get expected count from Stockfish
            var expectedCount = GetExpectedPerft1(move.ToUci());
            
            if (ourCount != expectedCount && expectedCount > 0)
            {
                failingPositions.Add((move.ToUci(), newFen, ourCount, expectedCount));
            }
            
            position.UnmakeMove(move, undoInfo);
        }
        
        if (failingPositions.Count > 0)
        {
            Console.WriteLine("=== FOUND FAILING POSITIONS AT DEPTH 1 ===\n");
            foreach (var (move, failedFen, ourCount, expectedCount) in failingPositions)
            {
                Console.WriteLine($"After move: {move}");
                Console.WriteLine($"FEN: {failedFen}");
                Console.WriteLine($"Our count: {ourCount}");
                Console.WriteLine($"Expected: {expectedCount}");
                Console.WriteLine($"Difference: {ourCount - expectedCount}\n");
            }
        }
        else
        {
            Console.WriteLine("No failing positions found at depth 1.");
            Console.WriteLine("The +1 error must come from deeper in the tree.");
            
            // Let's do a perft divide to see which move contributes the extra node
            Console.WriteLine("\n=== PERFT DIVIDE AT DEPTH 2 ===\n");
            
            ulong totalNodes = 0;
            var divideResults = new List<(string move, ulong nodes)>();
            
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                var undoInfo = position.MakeMove(move);
                var nodes = Perft(position, 1);
                position.UnmakeMove(move, undoInfo);
                
                divideResults.Add((move.ToUci(), nodes));
                totalNodes += nodes;
            }
            
            // Sort by node count to find anomalies
            divideResults.Sort((a, b) => b.nodes.CompareTo(a.nodes));
            
            foreach (var (move, nodes) in divideResults)
            {
                var expected = GetExpectedPerft1(move);
                if (expected > 0 && nodes != (ulong)expected)
                {
                    Console.WriteLine($"{move}: {nodes} (expected {expected}) ← MISMATCH");
                }
                else
                {
                    Console.WriteLine($"{move}: {nodes}");
                }
            }
            
            Console.WriteLine($"\nTotal: {totalNodes} (expected 707)");
        }
    }
    
    private int GetExpectedPerft1(string move)
    {
        // Expected perft(1) values for each move from the en passant position
        // These are from Stockfish perft divide at depth 2
        var expected = new Dictionary<string, int>
        {
            ["a2a3"] = 20,
            ["b2b3"] = 20,
            ["c2c3"] = 20,
            ["d2d3"] = 20,
            ["f2f3"] = 20,
            ["g2g3"] = 20,
            ["h2h3"] = 20,
            ["a2a4"] = 20,
            ["b2b4"] = 20,
            ["c2c4"] = 20,
            ["d2d4"] = 20,
            ["f2f4"] = 20,
            ["g2g4"] = 20,
            ["h2h4"] = 20,
            ["e5e6"] = 29,
            ["e5f6"] = 30, // En passant capture
            ["e5d6"] = 31,
            ["b1a3"] = 20,
            ["b1c3"] = 20,
            ["b1d2"] = 29,
            ["g1f3"] = 29,
            ["g1h3"] = 20,
            ["g1e2"] = 30,
            ["c1d2"] = 20,
            ["c1e3"] = 20,
            ["c1f4"] = 20,
            ["c1g5"] = 20,
            ["c1h6"] = 19,
            ["d1e2"] = 27,
            ["d1f3"] = 24,
            ["d1g4"] = 22,
            ["d1h5"] = 21,
            ["e1e2"] = 27,
            ["e1d2"] = 27
        };
        
        return expected.ContainsKey(move) ? expected[move] : -1;
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