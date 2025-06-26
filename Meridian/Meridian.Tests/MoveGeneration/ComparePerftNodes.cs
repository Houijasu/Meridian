#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System.Collections.Generic;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class ComparePerftNodes
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void ComparePerftDepth2()
    {
        // Expected values from standard perft results for this position at depth 2
        var expectedPerftDivide = new Dictionary<string, ulong>
        {
            ["a2a3"] = 29, ["a2a4"] = 29, ["b1a3"] = 29, ["b1c3"] = 29,
            ["b2b3"] = 29, ["b2b4"] = 29, ["c2c3"] = 29, ["c2c4"] = 29,
            ["d2d3"] = 29, ["d2d4"] = 29, ["e5d6"] = 30, ["e5f6"] = 29,  // en passant
            ["f2f3"] = 29, ["f2f4"] = 29, ["g1f3"] = 29, ["g1h3"] = 29,
            ["g2g3"] = 29, ["g2g4"] = 29, ["h2h3"] = 29, ["h2h4"] = 29,
            ["e1d2"] = 29, ["e1e2"] = 29, ["e1f1"] = 29,
            ["d1d2"] = 29, ["d1e2"] = 29, ["d1f3"] = 29, ["d1g4"] = 29, ["d1h5"] = 31,
            ["c1d2"] = 29, ["c1e3"] = 29, ["c1f4"] = 29, ["c1g5"] = 31, ["c1h6"] = 29,
            ["f1a6"] = 29, ["f1b5"] = 31, ["f1c4"] = 29, ["f1d3"] = 29, ["f1e2"] = 29
        };
        
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Get perft divide at depth 2
        var actualPerftDivide = PerftDivide(position, 2);
        
        Console.WriteLine("Move\tExpected\tActual\tDiff");
        Console.WriteLine("----\t--------\t------\t----");
        
        var totalExpected = 0UL;
        var totalActual = 0UL;
        
        // Compare all moves
        var allMoves = new HashSet<string>(expectedPerftDivide.Keys);
        foreach (var move in actualPerftDivide.Keys)
        {
            allMoves.Add(move);
        }
        
        foreach (var move in allMoves.OrderBy(m => m))
        {
            var expected = expectedPerftDivide.ContainsKey(move) ? expectedPerftDivide[move] : 0;
            var actual = actualPerftDivide.ContainsKey(move) ? actualPerftDivide[move] : 0;
            var diff = (long)actual - (long)expected;
            
            if (diff != 0)
            {
                Console.WriteLine($"{move}\t{expected}\t{actual}\t{diff:+#;-#;0}");
            }
            
            totalExpected += expected;
            totalActual += actual;
        }
        
        Console.WriteLine($"\nTotal:\t{totalExpected}\t{totalActual}\t{(long)totalActual - (long)totalExpected:+#;-#;0}");
        
        // Specific check for en passant
        if (actualPerftDivide.ContainsKey("e5f6"))
        {
            Console.WriteLine($"\nEn passant move e5f6 found with {actualPerftDivide["e5f6"]} nodes");
        }
        else
        {
            Console.WriteLine("\nEn passant move e5f6 NOT FOUND!");
        }
    }
    
    private Dictionary<string, ulong> PerftDivide(Position position, int depth)
    {
        var results = new Dictionary<string, ulong>();
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var nodes = depth > 1 ? Perft(position, depth - 1) : 1;
            position.UnmakeMove(move, undoInfo);
            results[move.ToUci()] = nodes;
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