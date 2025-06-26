#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class QuickPerftDivide
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void PerftDivideStartingPosition()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("Perft divide for starting position at depth 3:");
        Console.WriteLine("Expected total: 8902");
        
        var total = 0UL;
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        // Sort moves for consistent output
        var sortedMoves = new Move[moves.Count];
        for (int i = 0; i < moves.Count; i++)
        {
            sortedMoves[i] = moves[i];
        }
        Array.Sort(sortedMoves, (a, b) => string.Compare(a.ToUci(), b.ToUci()));
        
        foreach (var move in sortedMoves)
        {
            var undoInfo = position.MakeMove(move);
            var nodes = Perft(position, 2);
            position.UnmakeMove(move, undoInfo);
            
            total += nodes;
            Console.WriteLine($"{move.ToUci()}: {nodes}");
        }
        
        Console.WriteLine($"\nTotal: {total}");
        Console.WriteLine($"Expected: 8902");
        Console.WriteLine($"Difference: {(long)total - 8902}");
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