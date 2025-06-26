#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class PromotionPerftDivide
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CheckPromotionPerftDivide()
    {
        var positionResult = Position.FromFen("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("Promotion position perft divide at depth 3:");
        Console.WriteLine("Expected total: 4699");
        
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
        
        Console.WriteLine($"Total moves generated: {moves.Count}");
        
        // Count promotion moves
        var promotionMoves = 0;
        foreach (var move in sortedMoves)
        {
            if (move.ToUci().Length == 5) // Promotion moves have 5 characters (e.g., a7a8q)
            {
                promotionMoves++;
            }
        }
        Console.WriteLine($"Promotion moves: {promotionMoves}");
        Console.WriteLine();
        
        foreach (var move in sortedMoves)
        {
            var undoInfo = position.MakeMove(move);
            var nodes = Perft(position, 2);
            position.UnmakeMove(move, undoInfo);
            
            total += nodes;
            Console.WriteLine($"{move.ToUci()}: {nodes}");
        }
        
        Console.WriteLine($"\nTotal: {total}");
        Console.WriteLine($"Expected: 4699");
        Console.WriteLine($"Difference: {(long)total - 4699}");
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