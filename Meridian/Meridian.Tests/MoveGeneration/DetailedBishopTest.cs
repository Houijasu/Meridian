#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class DetailedBishopTest
{
    [TestMethod]
    public void TestBishopC1InPosition()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        Console.WriteLine("Board state:");
        PrintBoard(position);
        
        var c1 = Square.C1;
        var occupied = position.OccupiedSquares();
        
        Console.WriteLine($"\nBishop on c1: {position.GetPiece(c1)}");
        Console.WriteLine($"b2: {position.GetPiece(Square.B2)}");
        Console.WriteLine($"d2: {position.GetPiece(Square.D2)}");
        Console.WriteLine($"e3: {position.GetPiece(Square.E3)}");
        Console.WriteLine($"a3: {position.GetPiece(Square.A3)}");
        
        // Get bishop attacks
        var bishopAttacks = MagicBitboards.GetBishopAttacks(c1, occupied);
        Console.WriteLine($"\nBishop attacks from c1: {GetSquareList(bishopAttacks)}");
        
        // Now let's manually trace what should happen
        // From c1, the diagonals are:
        // - Northeast: d2, e3, f4, g5, h6
        // - Northwest: b2, a3
        // - Southeast/Southwest: blocked by edge
        
        // Let's check if the occupancy is correct
        Console.WriteLine($"\nOccupied squares bitmap: 0x{occupied.Value:X16}");
        Console.WriteLine($"Is b2 occupied in bitmap: {(occupied & Square.B2.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"Is d2 occupied in bitmap: {(occupied & Square.D2.ToBitboard()).IsNotEmpty()}");
        
        // Test with a different position - remove the pawn on d2
        position.RemovePiece(Square.D2);
        occupied = position.OccupiedSquares();
        bishopAttacks = MagicBitboards.GetBishopAttacks(c1, occupied);
        Console.WriteLine($"\nAfter removing d2 pawn:");
        Console.WriteLine($"Bishop attacks from c1: {GetSquareList(bishopAttacks)}");
        
        // Now bishop should see further along the diagonal
    }
    
    private void PrintBoard(Position position)
    {
        Console.WriteLine("\n  a b c d e f g h");
        for (int rank = 7; rank >= 0; rank--)
        {
            Console.Write($"{rank + 1} ");
            for (int file = 0; file < 8; file++)
            {
                var piece = position.GetPiece(SquareExtensions.FromFileRank(file, rank));
                var ch = GetPieceChar(piece);
                Console.Write($"{ch} ");
            }
            Console.WriteLine();
        }
    }
    
    private char GetPieceChar(Piece piece) => piece switch
    {
        Piece.WhitePawn => 'P',
        Piece.WhiteKnight => 'N',
        Piece.WhiteBishop => 'B',
        Piece.WhiteRook => 'R',
        Piece.WhiteQueen => 'Q',
        Piece.WhiteKing => 'K',
        Piece.BlackPawn => 'p',
        Piece.BlackKnight => 'n',
        Piece.BlackBishop => 'b',
        Piece.BlackRook => 'r',
        Piece.BlackQueen => 'q',
        Piece.BlackKing => 'k',
        _ => '.'
    };
    
    private string GetSquareList(Bitboard bb)
    {
        var squares = new List<string>();
        var temp = bb;
        while (temp.IsNotEmpty())
        {
            var sq = (Square)temp.GetLsbIndex();
            squares.Add(sq.ToAlgebraic());
            temp = temp.RemoveLsb();
        }
        return string.Join(", ", squares);
    }
}