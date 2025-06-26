#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class DebugMoveGeneration
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugMissingMoves()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {fen}");
        PrintBoard(position);
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nTotal moves generated: {moves.Count}");
        
        // Check specific squares
        Console.WriteLine("\nChecking pieces:");
        Console.WriteLine($"c1 (should be White Bishop): {position.GetPiece(Square.C1)}");
        Console.WriteLine($"d1 (should be White Queen): {position.GetPiece(Square.D1)}");
        Console.WriteLine($"e1 (should be White King): {position.GetPiece(Square.E1)}");
        Console.WriteLine($"e5 (should be White Pawn): {position.GetPiece(Square.E5)}");
        Console.WriteLine($"d5 (should be Black Pawn): {position.GetPiece(Square.D5)}");
        
        // List all moves by piece
        var movesByPiece = new Dictionary<string, List<string>>();
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var piece = position.GetPiece(move.From);
            var key = $"{move.From.ToAlgebraic()} ({piece})";
            
            if (!movesByPiece.ContainsKey(key))
            {
                movesByPiece[key] = new List<string>();
            }
            
            movesByPiece[key].Add(move.ToUci());
        }
        
        Console.WriteLine("\nMoves by piece:");
        foreach (var (piece, pieceMoves) in movesByPiece.OrderBy(x => x.Key))
        {
            Console.WriteLine($"\n{piece}: {string.Join(", ", pieceMoves)}");
        }
        
        // Check specifically for missing moves
        Console.WriteLine("\nChecking for specific moves:");
        CheckForMove(moves, "c1d2", "Bishop from c1 to d2");
        CheckForMove(moves, "e5d6", "Pawn captures from e5 to d6");
        CheckForMove(moves, "e5f6", "En passant from e5 to f6");
        CheckForMove(moves, "d1d2", "Queen from d1 to d2");
        CheckForMove(moves, "e1d2", "King from e1 to d2");
        
        // Generate pseudo-legal moves to see if they're being filtered
        Console.WriteLine("\nGenerating pseudo-legal moves to compare...");
        GeneratePseudoLegalMoves(position);
    }
    
    private void CheckForMove(MoveList moves, string uci, string description)
    {
        var found = false;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == uci)
            {
                found = true;
                break;
            }
        }
        Console.WriteLine($"{description}: {(found ? "FOUND" : "MISSING")}");
    }
    
    private void GeneratePseudoLegalMoves(Position position)
    {
        // Simple pseudo-legal move generation for comparison
        var whitePieces = position.GetBitboard(Color.White);
        var occupied = position.OccupiedSquares();
        
        var pieceCount = 0;
        var temp = whitePieces;
        while (temp.IsNotEmpty())
        {
            var from = (Square)temp.GetLsbIndex();
            var piece = position.GetPiece(from);
            pieceCount++;
            temp = temp.RemoveLsb();
        }
        
        Console.WriteLine($"White has {pieceCount} pieces");
        
        // Check bishop on c1
        var c1 = Square.C1;
        var bishopOnC1 = position.GetPiece(c1);
        Console.WriteLine($"\nBishop on c1: {bishopOnC1}");
        
        if (bishopOnC1 == Piece.WhiteBishop)
        {
            // Manually calculate bishop moves from c1
            var bishopAttacks = MagicBitboards.GetBishopAttacks(c1, occupied);
            var bishopTargets = bishopAttacks & ~position.GetBitboard(Color.White);
            
            Console.WriteLine($"Occupied squares: {GetSquareList(occupied)}");
            Console.WriteLine($"Occupied value: 0x{occupied.Value:X16}");
            Console.WriteLine($"Bishop attacks (raw): {GetSquareList(bishopAttacks)}");
            Console.WriteLine($"Bishop attacks value: 0x{bishopAttacks.Value:X16}");
            Console.WriteLine($"White pieces: {GetSquareList(position.GetBitboard(Color.White))}");
            Console.WriteLine($"Bishop from c1 can attack: {GetSquareList(bishopTargets)}");
            
            // Check specific squares
            Console.WriteLine($"\nChecking diagonal from c1:");
            Console.WriteLine($"b2 occupied: {!position.IsEmpty(Square.B2)} (piece: {position.GetPiece(Square.B2)})");
            Console.WriteLine($"d2 occupied: {!position.IsEmpty(Square.D2)} (piece: {position.GetPiece(Square.D2)})");
            
            // Try a simpler test - empty board
            Console.WriteLine($"\nTesting bishop on empty board:");
            var emptyBoardAttacks = MagicBitboards.GetBishopAttacks(c1, Bitboard.Empty);
            Console.WriteLine($"Bishop attacks on empty board: {GetSquareList(emptyBoardAttacks)}");
        }
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