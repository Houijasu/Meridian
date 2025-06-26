#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;

namespace Meridian.Tests.Perft;

[TestClass]
public class AnalyzePromotionDivide
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void DividePromotionPosition()
    {
        // Perft divide to see which moves contribute to the overcount
        var positionResult = Position.FromFen("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine("\nPerft divide at depth 3:");
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        var totalNodes = 0UL;
        var results = new List<(string move, ulong nodes)>();
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var nodes = Perft(position, 2); // depth 3 - 1
            position.UnmakeMove(move, undoInfo);
            
            results.Add((move.ToUci(), nodes));
            totalNodes += nodes;
        }
        
        // Sort by node count descending
        results.Sort((a, b) => b.nodes.CompareTo(a.nodes));
        
        foreach (var (move, nodes) in results)
        {
            Console.WriteLine($"{move}: {nodes}");
        }
        
        Console.WriteLine($"\nTotal: {totalNodes} (expected 4,699)");
        
        // Now do depth 4 divide
        Console.WriteLine("\n\nPerft divide at depth 4:");
        
        totalNodes = 0UL;
        results.Clear();
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var nodes = Perft(position, 3); // depth 4 - 1
            position.UnmakeMove(move, undoInfo);
            
            results.Add((move.ToUci(), nodes));
            totalNodes += nodes;
        }
        
        // Sort by node count descending
        results.Sort((a, b) => b.nodes.CompareTo(a.nodes));
        
        // Expected values for depth 4 (from standard perft):
        var expected = new Dictionary<string, ulong>
        {
            ["a7a8q"] = 4510,
            ["a7a8r"] = 4510,
            ["a7a8b"] = 4227,
            ["a7a8n"] = 4227,
            ["b7b8q"] = 4510,
            ["b7b8r"] = 4510,
            ["b7b8b"] = 4227,
            ["b7b8n"] = 4227,
            ["c7c8q"] = 4510,
            ["c7c8r"] = 4510,
            ["c7c8b"] = 4227,
            ["c7c8n"] = 4227,
            ["e2d1"] = 3991,
            ["e2d2"] = 3991,
            ["e2f2"] = 4042,
            ["e2d3"] = 3991,
            ["e2e3"] = 3991,
            ["e2f3"] = 3991
        };
        
        foreach (var (move, nodes) in results)
        {
            var exp = expected.ContainsKey(move) ? expected[move] : 0;
            var diff = (long)nodes - (long)exp;
            if (diff != 0)
            {
                Console.WriteLine($"{move}: {nodes} (expected {exp}, diff: {diff:+#;-#;0})");
            }
            else
            {
                Console.WriteLine($"{move}: {nodes}");
            }
        }
        
        Console.WriteLine($"\nTotal: {totalNodes} (expected 73,683, diff: +{totalNodes - 73683})");
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