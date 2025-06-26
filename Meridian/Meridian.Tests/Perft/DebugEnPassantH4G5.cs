#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugEnPassantH4G5
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestH4G5EnPassant()
    {
        // Position after 1.h4 g5
        var positionResult = Position.FromFen("rnbqkbnr/pppppp1p/8/6p1/7P/8/PPPPPPP1/RNBQKBNR w KQkq g6 0 2");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine($"En passant square: {position.EnPassantSquare} ({position.EnPassantSquare.ToAlgebraic()})");
        Console.WriteLine($"White pawn on h4: {position.GetPiece(Square.H4) == Piece.WhitePawn}");
        Console.WriteLine($"Black pawn on g5: {position.GetPiece(Square.G5) == Piece.BlackPawn}");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nTotal moves: {moves.Count}");
        
        // Look for h4 moves
        var h4Moves = 0;
        var h4xg5Found = false;
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.From == Square.H4)
            {
                h4Moves++;
                Console.WriteLine($"H4 move: {move.ToUci()} (flags: {move.Flags})");
                
                if (move.To == Square.G5 && (move.Flags & MoveType.EnPassant) != 0)
                {
                    h4xg5Found = true;
                }
            }
        }
        
        Console.WriteLine($"\nH4 moves found: {h4Moves}");
        Console.WriteLine($"h4xg5 en passant found: {h4xg5Found}");
        
        // Test the pawn attacks
        var pawnAttacks = AttackTables.PawnAttacks(Square.G6, Color.Black);
        Console.WriteLine($"\nBlack pawn attacks from g6: {GetSquareList(pawnAttacks)}");
        
        var whitePawns = position.GetBitboard(Color.White, PieceType.Pawn);
        Console.WriteLine($"White pawns: {GetSquareList(whitePawns)}");
        
        var attackers = pawnAttacks & whitePawns;
        Console.WriteLine($"White pawns that can attack g6: {GetSquareList(attackers)}");
        
        Assert.IsTrue(h4xg5Found, "h4xg5 en passant capture should be generated");
    }
    
    private string GetSquareList(Bitboard bb)
    {
        var squares = new System.Collections.Generic.List<string>();
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