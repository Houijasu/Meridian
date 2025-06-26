#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugEnPassantIssue
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugEnPassantMoveGeneration()
    {
        // Position with en passant: after 1.e4 e6 2.e5 f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        Console.WriteLine($"Initial position: {position.ToFen()}");
        Console.WriteLine($"En passant square: {position.EnPassantSquare}");
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nTotal moves from position: {moves.Count}");
        
        // Count en passant moves
        int enPassantCount = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if ((move.Flags & MoveType.EnPassant) != MoveType.None)
            {
                enPassantCount++;
                Console.WriteLine($"En passant move found: {move.ToUci()}");
            }
        }
        
        Console.WriteLine($"Total en passant moves: {enPassantCount}");
        
        // Now check if we're generating duplicate moves
        var uniqueMoves = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < moves.Count; i++)
        {
            var moveStr = moves[i].ToUci();
            if (!uniqueMoves.Add(moveStr))
            {
                Console.WriteLine($"DUPLICATE MOVE FOUND: {moveStr}");
            }
        }
        
        // Make the en passant move and check the resulting position
        Move? epMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if ((moves[i].Flags & MoveType.EnPassant) != MoveType.None)
            {
                epMove = moves[i];
                break;
            }
        }
        
        if (epMove.HasValue)
        {
            var undoInfo = position.MakeMove(epMove.Value);
            Console.WriteLine($"\nAfter en passant move {epMove.Value.ToUci()}:");
            Console.WriteLine($"FEN: {position.ToFen()}");
            
            // Check what pieces are on relevant squares
            Console.WriteLine($"Piece at e5: {position.GetPiece(Square.E5)}");
            Console.WriteLine($"Piece at f5: {position.GetPiece(Square.F5)}");
            Console.WriteLine($"Piece at f6: {position.GetPiece(Square.F6)}");
            
            position.UnmakeMove(epMove.Value, undoInfo);
            Console.WriteLine($"\nAfter unmake:");
            Console.WriteLine($"FEN: {position.ToFen()}");
            Console.WriteLine($"Piece at e5: {position.GetPiece(Square.E5)}");
            Console.WriteLine($"Piece at f5: {position.GetPiece(Square.F5)}");
            Console.WriteLine($"Piece at f6: {position.GetPiece(Square.F6)}");
        }
    }
}