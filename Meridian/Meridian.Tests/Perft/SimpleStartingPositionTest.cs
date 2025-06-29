#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class SimpleStartingPositionTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestStartingPositionPerft()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        // Test each depth
        var results = new (int depth, ulong expected, ulong actual)[6];
        
        for (int depth = 1; depth <= 4; depth++)
        {
            var nodes = Perft(position, depth);
            var expected = depth switch
            {
                1 => 20UL,
                2 => 400UL,
                3 => 8902UL,
                4 => 197281UL,
                _ => 0UL
            };
            
            results[depth - 1] = (depth, expected, nodes);
        }

        // Force output
        var output = new System.Text.StringBuilder();
        output.AppendLine("Starting position perft results:");
        output.AppendLine("Depth | Expected    | Actual      | Diff");
        output.AppendLine("------|-------------|-------------|--------");
        
        bool hasError = false;
        foreach (var (depth, expected, actual) in results)
        {
            if (expected == 0) continue;
            var diff = (long)actual - (long)expected;
            output.AppendLine($"  {depth}   | {expected,11} | {actual,11} | {diff,6:+#;-#;0}");
            if (diff != 0) hasError = true;
        }

        if (hasError)
        {
            Assert.Fail(output.ToString());
        }
        else
        {
            Console.WriteLine(output.ToString());
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