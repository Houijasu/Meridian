#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class InvestigateF1B5
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void InvestigateMovesAfterF1B5()
    {
        // Position after f1b5
        var fenResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/1B1pPp2/8/8/PPPP1PPP/RNBQK1NR b KQkq - 1 3");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        Console.WriteLine($"Position: {position.ToFen()}");
        
        // Check if Black king is in check
        var blackKing = position.GetBitboard(Color.Black, PieceType.King);
        if (blackKing.IsNotEmpty())
        {
            var kingSquare = (Square)blackKing.GetLsbIndex();
            var isInCheck = MoveGenerator.IsSquareAttacked(position, kingSquare, Color.White);
            Console.WriteLine($"Black king on {kingSquare} is in check: {isInCheck}");
            
            // Check what's attacking the king
            var attackers = MoveGenerator.GetAttackers(position, kingSquare, position.OccupiedSquares());
            Console.WriteLine($"Attackers of black king: {attackers}");
            
            // Check bishop on b5
            var b5Bishop = position.GetPiece(Square.B5);
            Console.WriteLine($"Piece on b5: {b5Bishop}");
            
            // Manually check if bishop can attack e8
            var bishopAttacks = MagicBitboards.GetBishopAttacks(Square.B5, position.OccupiedSquares());
            Console.WriteLine($"Bishop on b5 attacks: {bishopAttacks}");
            Console.WriteLine($"Does bishop attack e8? {(bishopAttacks & Square.E8.ToBitboard()).IsNotEmpty()}");
        }
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nMoves generated: {moves.Count}");
        for (int i = 0; i < moves.Count; i++)
        {
            Console.WriteLine($"  {moves[i].ToUci()}");
        }
        
        // Let's manually check what moves should be legal
        // When in check, only moves that:
        // 1. Move the king out of check
        // 2. Block the check
        // 3. Capture the checking piece
        
        Console.WriteLine("\nExpected legal moves when in check from Bb5:");
        Console.WriteLine("1. Kd7 - king moves");
        Console.WriteLine("2. Kf7 - king moves");  
        Console.WriteLine("3. c6 - blocks check");
        Console.WriteLine("4. Nc6 - blocks check");
        Console.WriteLine("5. Qe7 - blocks check");
        Console.WriteLine("6. Be7 - blocks check");
    }
}