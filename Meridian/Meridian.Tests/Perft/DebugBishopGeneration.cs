#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class DebugBishopGeneration
{
    private readonly MoveGenerator _moveGenerator = new();
    private const string KiwipeteFen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

    [TestMethod]
    public void AnalyzeBishopMoves()
    {
        var positionResult = Position.FromFen(KiwipeteFen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        Console.WriteLine("Board position:");
        PrintBoard(position);
        Console.WriteLine();

        // Check bishop positions
        var whiteBishops = position.GetBitboard(Color.White, PieceType.Bishop);
        Console.WriteLine("White bishops:");
        PrintBitboard(whiteBishops);

        // Generate moves and look for bishop moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        Console.WriteLine("\nBishop moves generated:");
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var piece = position.GetPiece(move.From);
            if (piece.Type() == PieceType.Bishop && piece.GetColor() == Color.White)
            {
                Console.WriteLine($"  {move.ToUci()} - from {move.From} to {move.To}");
                
                // Check if this is one of the illegal moves
                var uci = move.ToUci();
                if (uci == "d2c1" || uci == "d2e3" || uci == "d2f4" || uci == "d2g5" || uci == "d2h6" ||
                    uci == "e2a6" || uci == "e2b5")
                {
                    Console.WriteLine($"    ILLEGAL MOVE! Bishop on {move.From} cannot reach {move.To}");
                    CheckBishopPath(position, move.From, move.To);
                }
            }
        }
    }

    private void CheckBishopPath(Position position, Square from, Square to)
    {
        var occupancy = position.OccupiedSquares();
        var attacks = MagicBitboards.GetBishopAttacks(from, occupancy);
        
        Console.WriteLine($"    Bishop attacks from {from}:");
        PrintBitboard(attacks);
        Console.WriteLine($"    Target square {to} is{((attacks & to.ToBitboard()).IsNotEmpty() ? "" : " NOT")} in attack set");
    }

    private void PrintBoard(Position position)
    {
        for (int rank = 7; rank >= 0; rank--)
        {
            Console.Write($"{rank + 1} ");
            for (int file = 0; file < 8; file++)
            {
                var square = (Square)(rank * 8 + file);
                var piece = position.GetPiece(square);
                Console.Write(PieceToChar(piece) + " ");
            }
            Console.WriteLine();
        }
        Console.WriteLine("  a b c d e f g h");
    }

    private char PieceToChar(Piece piece)
    {
        if (piece == Piece.None) return '.';
        
        var ch = piece.Type() switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => '?'
        };
        
        return piece.GetColor() == Color.White ? char.ToUpper(ch) : ch;
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