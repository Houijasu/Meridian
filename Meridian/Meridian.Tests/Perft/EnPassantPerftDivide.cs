#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class EnPassantPerftDivide
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CheckEnPassantPerftDivide()
    {
        var positionResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("En passant position perft divide at depth 3:");
        Console.WriteLine("Expected total: 27837");
        
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
            
            // Special check for en passant move
            if (move.ToUci() == "e5f6")
            {
                Console.WriteLine("  -> This is the en passant capture!");
            }
        }
        
        Console.WriteLine($"\nTotal: {total}");
        Console.WriteLine($"Expected: 27837");
        Console.WriteLine($"Difference: {(long)total - 27837}");
        
        // Check if en passant move exists
        var epMove = sortedMoves.FirstOrDefault(m => m.ToUci() == "e5f6");
        if (epMove.ToUci() == null)
        {
            Console.WriteLine("\nWARNING: En passant move e5f6 not found!");
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