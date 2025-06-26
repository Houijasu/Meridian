#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class EnPassantMakeUnmakeTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestEnPassantMakeUnmake()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        Console.WriteLine($"Initial FEN: {fen}");
        Console.WriteLine($"Initial position:");
        PrintPosition(position);
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        // Find the en passant move
        Move? epMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].IsEnPassant)
            {
                epMove = moves[i];
                break;
            }
        }
        
        Assert.IsTrue(epMove.HasValue, "Should find en passant move");
        Console.WriteLine($"\nEn passant move: {epMove.Value.ToUci()}");
        
        // Check the board state before the move
        var e5Piece = position.GetPiece(Square.E5);
        var f5Piece = position.GetPiece(Square.F5);
        var f6Piece = position.GetPiece(Square.F6);
        
        Console.WriteLine($"\nBefore move:");
        Console.WriteLine($"e5: {e5Piece}");
        Console.WriteLine($"f5: {f5Piece}");
        Console.WriteLine($"f6: {f6Piece}");
        
        // Make the en passant move
        var undoInfo = position.MakeMove(epMove.Value);
        
        Console.WriteLine($"\nAfter making move:");
        PrintPosition(position);
        
        // Check the board state after the move
        e5Piece = position.GetPiece(Square.E5);
        f5Piece = position.GetPiece(Square.F5);
        f6Piece = position.GetPiece(Square.F6);
        
        Console.WriteLine($"\nAfter move:");
        Console.WriteLine($"e5: {e5Piece}");
        Console.WriteLine($"f5: {f5Piece}");
        Console.WriteLine($"f6: {f6Piece}");
        
        // The pawn should have moved from e5 to f6
        Assert.AreEqual(Piece.None, e5Piece, "e5 should be empty");
        Assert.AreEqual(Piece.None, f5Piece, "f5 should be empty (captured pawn)");
        Assert.AreEqual(Piece.WhitePawn, f6Piece, "f6 should have white pawn");
        
        // Now generate moves for Black
        Span<Move> blackMoveBuffer = stackalloc Move[218];
        var blackMoves = new MoveList(blackMoveBuffer);
        _moveGenerator.GenerateMoves(position, ref blackMoves);
        
        Console.WriteLine($"\nBlack has {blackMoves.Count} legal moves after exf6");
        
        // Unmake the move
        position.UnmakeMove(epMove.Value, undoInfo);
        
        Console.WriteLine($"\nAfter unmaking move:");
        PrintPosition(position);
        
        // Check the board state after unmaking
        e5Piece = position.GetPiece(Square.E5);
        f5Piece = position.GetPiece(Square.F5);
        f6Piece = position.GetPiece(Square.F6);
        
        Console.WriteLine($"\nAfter unmake:");
        Console.WriteLine($"e5: {e5Piece}");
        Console.WriteLine($"f5: {f5Piece}");
        Console.WriteLine($"f6: {f6Piece}");
        
        Assert.AreEqual(Piece.WhitePawn, e5Piece, "e5 should have white pawn");
        Assert.AreEqual(Piece.BlackPawn, f5Piece, "f5 should have black pawn");
        Assert.AreEqual(Piece.None, f6Piece, "f6 should be empty");
        
        // Verify the position is fully restored
        Assert.AreEqual(fen, position.ToFen(), "Position should be fully restored");
    }
    
    private void PrintPosition(Position position)
    {
        Console.WriteLine("  a b c d e f g h");
        for (int rank = 7; rank >= 0; rank--)
        {
            Console.Write($"{rank + 1} ");
            for (int file = 0; file < 8; file++)
            {
                var piece = position.GetPiece(SquareExtensions.FromFileRank(file, rank));
                var ch = piece switch
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
                Console.Write($"{ch} ");
            }
            Console.WriteLine();
        }
    }
}