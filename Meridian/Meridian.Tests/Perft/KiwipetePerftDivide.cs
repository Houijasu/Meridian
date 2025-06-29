#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
public class KiwipetePerftDivide
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CheckKiwipetePerftDivide()
    {
        var positionResult = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("Kiwipete position perft divide at depth 1:");
        Console.WriteLine("Expected total: 48");
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Total moves generated: {moves.Count}");
        
        // Count castling moves
        var castlingMoves = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            var moveStr = moves[i].ToUci();
            if ((moveStr == "e1g1" || moveStr == "e1c1") && position.GetPiece(Square.E1) == Piece.WhiteKing)
            {
                castlingMoves++;
                Console.WriteLine($"Castling move found: {moveStr}");
            }
        }
        Console.WriteLine($"Castling moves: {castlingMoves}");
        
        // Sort moves for consistent output
        var sortedMoves = new Move[moves.Count];
        for (int i = 0; i < moves.Count; i++)
        {
            sortedMoves[i] = moves[i];
        }
        Array.Sort(sortedMoves, (a, b) => string.Compare(a.ToUci(), b.ToUci()));
        
        Console.WriteLine("\nMove list:");
        foreach (var move in sortedMoves)
        {
            Console.WriteLine($"  {move.ToUci()}");
        }
        
        Console.WriteLine($"\nTotal moves: {moves.Count}");
        Console.WriteLine($"Expected: 48");
        Console.WriteLine($"Difference: {moves.Count - 48}");
    }
}