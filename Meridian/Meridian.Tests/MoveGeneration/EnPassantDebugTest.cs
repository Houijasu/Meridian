#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System.Linq;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class EnPassantDebugTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void Debug_EnPassant_Position()
    {
        // Position: White to move, Black just played f7-f5
        // White pawn on e5 can capture en passant on f6
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        // Find en passant moves
        var enPassantMoves = new List<Move>();
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].IsEnPassant)
            {
                enPassantMoves.Add(moves[i]);
            }
        }
        
        // Debug output
        Console.WriteLine($"Total moves generated: {moves.Count}");
        Console.WriteLine($"En passant moves found: {enPassantMoves.Count}");
        
        foreach (var move in enPassantMoves)
        {
            Console.WriteLine($"En passant move: {move.From.ToAlgebraic()} -> {move.To.ToAlgebraic()}");
        }
        
        // We should have exactly one en passant move: e5xf6
        Assert.AreEqual(1, enPassantMoves.Count, "Should have exactly one en passant move");
        
        var epMove = enPassantMoves[0];
        Assert.AreEqual(Square.E5, epMove.From, "En passant should be from e5");
        Assert.AreEqual(Square.F6, epMove.To, "En passant should be to f6");
        
        // Let's also check what square we think the captured pawn is on
        var us = position.SideToMove;
        var captureSquare = us == Color.White ? epMove.To - 8 : epMove.To + 8;
        Console.WriteLine($"Calculated capture square: {((Square)captureSquare).ToAlgebraic()}");
        Assert.AreEqual(Square.F5, (Square)captureSquare, "Captured pawn should be on f5");
    }
    
    [TestMethod]
    public void Test_PawnAttacks_Direction()
    {
        // Let's understand how PawnAttacks works
        var f6 = Square.F6;
        
        // Get pawn attacks from f6 by White
        var whiteAttacksFromF6 = AttackTables.PawnAttacks(f6, Color.White);
        Console.WriteLine($"White pawn attacks FROM f6: {GetSquareList(whiteAttacksFromF6)}");
        
        // Get pawn attacks from f6 by Black  
        var blackAttacksFromF6 = AttackTables.PawnAttacks(f6, Color.Black);
        Console.WriteLine($"Black pawn attacks FROM f6: {GetSquareList(blackAttacksFromF6)}");
        
        // What we actually need: squares that can attack f6
        // For White pawns to attack f6, they must be on e5 or g5
        var e5 = Square.E5;
        var g5 = Square.G5;
        
        var attacksFromE5 = AttackTables.PawnAttacks(e5, Color.White);
        var attacksFromG5 = AttackTables.PawnAttacks(g5, Color.White);
        
        Console.WriteLine($"White pawn attacks from e5: {GetSquareList(attacksFromE5)}");
        Console.WriteLine($"White pawn attacks from g5: {GetSquareList(attacksFromG5)}");
        
        // This is what the move generator is doing - it's asking for attacks FROM f6 by Black
        var attacksFromF6ByBlack = AttackTables.PawnAttacks(f6, Color.Black);
        Console.WriteLine($"Pawn attacks FROM f6 by Black (what the code uses): {GetSquareList(attacksFromF6ByBlack)}");
        
        Assert.IsTrue((attacksFromE5 & f6.ToBitboard()).IsNotEmpty(), "e5 should attack f6");
        Assert.IsTrue((attacksFromG5 & f6.ToBitboard()).IsNotEmpty(), "g5 should attack f6");
    }
    
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