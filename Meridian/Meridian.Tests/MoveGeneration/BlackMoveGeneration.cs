#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class BlackMoveGeneration
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestBlackMovesAfterWhite()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Make a simple White move (a2-a3)
        var move = new Move(Square.A2, Square.A3, MoveType.None);
        var undoInfo = position.MakeMove(move);
        
        Console.WriteLine("After White plays a2-a3:");
        PrintBoard(position);
        Console.WriteLine($"\nSide to move: {position.SideToMove}");
        Console.WriteLine($"En passant square: {position.EnPassantSquare.ToAlgebraic()}");
        
        // Generate Black's moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nBlack has {moves.Count} legal moves");
        
        // Group moves by piece
        var movesByPiece = new Dictionary<string, int>();
        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            var piece = position.GetPiece(m.From);
            var key = piece.ToString();
            if (!movesByPiece.ContainsKey(key))
                movesByPiece[key] = 0;
            movesByPiece[key]++;
        }
        
        Console.WriteLine("\nMoves by piece type:");
        foreach (var kvp in movesByPiece.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
        
        // Check specific pieces
        Console.WriteLine("\nChecking specific Black pieces:");
        Console.WriteLine($"Black bishop on c8: {position.GetPiece(Square.C8)}");
        Console.WriteLine($"Black bishop on f8: {position.GetPiece(Square.F8)}");
        Console.WriteLine($"Black queen on d8: {position.GetPiece(Square.D8)}");
        
        // Count bishop moves
        var bishopMoveCount = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            var piece = position.GetPiece(m.From);
            if (piece == Piece.BlackBishop)
            {
                bishopMoveCount++;
                Console.WriteLine($"Bishop move: {m.From.ToAlgebraic()} -> {m.To.ToAlgebraic()}");
            }
        }
        Console.WriteLine($"\nTotal bishop moves: {bishopMoveCount}");
        
        // The expected count is 29, we're getting 24, missing 5 moves
        // Let's check if Black's bishops are blocked too
        var c8 = Square.C8;
        var f8 = Square.F8;
        
        var c8Attacks = MagicBitboards.GetBishopAttacks(c8, position.OccupiedSquares());
        var f8Attacks = MagicBitboards.GetBishopAttacks(f8, position.OccupiedSquares());
        
        Console.WriteLine($"\nBishop on c8 attacks: {GetSquareList(c8Attacks)}");
        Console.WriteLine($"Bishop on f8 attacks: {GetSquareList(f8Attacks)}");
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