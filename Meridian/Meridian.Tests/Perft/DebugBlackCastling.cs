#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugBlackCastling
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CheckBlackQueensideCastling()
    {
        // Position with black to move and queenside castling rights
        var positionResult = Position.FromFen("r3k3/1K6/8/8/8/8/8/8 b q - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine($"Black king on: e8");
        Console.WriteLine($"Black rook on: a8");
        Console.WriteLine($"Castling rights: {position.CastlingRights}");
        Console.WriteLine();
        
        // Check squares between king and rook
        Console.WriteLine("Squares between king and rook for O-O-O:");
        Console.WriteLine($"b8: {(position.GetPiece(Square.B8) == Piece.None ? "empty ✓" : "occupied ❌")}");
        Console.WriteLine($"c8: {(position.GetPiece(Square.C8) == Piece.None ? "empty ✓" : "occupied ❌")}");
        Console.WriteLine($"d8: {(position.GetPiece(Square.D8) == Piece.None ? "empty ✓" : "occupied ❌")}");
        
        // Check if squares are attacked
        Console.WriteLine("\nSquares under attack check:");
        var whiteKingSquare = Square.B7;
        var whiteKingAttacks = AttackTables.KingAttacks(whiteKingSquare);
        Console.WriteLine($"White king on b7 attacks: {GetSquareList(whiteKingAttacks)}");
        
        Console.WriteLine($"e8 attacked: {(whiteKingAttacks & Square.E8.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"d8 attacked: {(whiteKingAttacks & Square.D8.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"c8 attacked: {(whiteKingAttacks & Square.C8.ToBitboard()).IsNotEmpty()}");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nBlack has {moves.Count} moves:");
        
        var castlingFound = false;
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.From == Square.E8)
            {
                Console.WriteLine($"{move.ToUci()} (flags: {move.Flags})");
                if ((move.Flags & MoveType.Castling) != 0)
                {
                    castlingFound = true;
                    Console.WriteLine("   ^ CASTLING O-O-O!");
                }
            }
        }
        
        Console.WriteLine($"\nCastling O-O-O found: {castlingFound}");
        Console.WriteLine("\nExpected: O-O-O should be possible since:");
        Console.WriteLine("- Black has queenside castling rights");
        Console.WriteLine("- Squares b8, c8, d8 are empty");
        Console.WriteLine("- e8, d8, c8 are not attacked by white");
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