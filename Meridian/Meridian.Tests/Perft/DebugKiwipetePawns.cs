#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class DebugKiwipetePawns
{
    private readonly MoveGenerator _moveGenerator = new();
    private const string KiwipeteFen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

    [TestMethod]
    public void DebugPawnPositions()
    {
        var positionResult = Position.FromFen(KiwipeteFen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        Console.WriteLine($"Analyzing pawn positions in Kiwipete:");
        Console.WriteLine();

        // Check what's on the squares
        var squares = new[] { "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2", "a3", "b3", "c3", "d3", "e3", "f3", "g3", "h3" };
        
        foreach (var sq in squares)
        {
            var square = ParseSquare(sq);
            var piece = position.GetPiece(square);
            if (piece != Piece.None)
            {
                Console.WriteLine($"{sq}: {piece}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("White pawns bitboard:");
        var whitePawns = position.GetBitboard(Color.White, PieceType.Pawn);
        PrintBitboard(whitePawns);

        Console.WriteLine();
        Console.WriteLine("Expected pawn moves:");
        Console.WriteLine("c2c3, h2h3, h2h4 should be legal");
        Console.WriteLine("d2 and e2 have bishops, not pawns!");
        Console.WriteLine("a2 can only move to a3 (a4 would require a3 to be empty)");
        Console.WriteLine("g2 is blocked by black pawn on h3");

        // Generate moves and check for pawn moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        Console.WriteLine();
        Console.WriteLine("Generated pawn moves:");
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var from = move.From;
            var piece = position.GetPiece(from);
            if (piece.Type() == PieceType.Pawn)
            {
                Console.WriteLine($"  {move.ToUci()}");
            }
        }
    }

    private Square ParseSquare(string square)
    {
        var file = square[0] - 'a';
        var rank = square[1] - '1';
        return (Square)(rank * 8 + file);
    }

    private void PrintBitboard(Bitboard bb)
    {
        for (int rank = 7; rank >= 0; rank--)
        {
            Console.Write($"{rank + 1} ");
            for (int file = 0; file < 8; file++)
            {
                var square = (Square)(rank * 8 + file);
                Console.Write((bb & square.ToBitboard()).IsNotEmpty() ? "1 " : ". ");
            }
            Console.WriteLine();
        }
        Console.WriteLine("  a b c d e f g h");
    }
}