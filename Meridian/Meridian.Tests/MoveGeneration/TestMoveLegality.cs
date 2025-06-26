#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System.Reflection;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class TestMoveLegality
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestSpecificMoveLegality()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Test specific moves that should be legal
        var testMoves = new[]
        {
            ("c1", "d2", "Bishop c1-d2"),
            ("e5", "d6", "Pawn captures e5xd6"),
            ("e5", "f6", "En passant e5xf6"),
            ("d1", "d2", "Queen d1-d2"),
            ("e1", "d2", "King e1-d2"),
            ("c1", "e3", "Bishop c1-e3")
        };
        
        // Use reflection to call the private IsMoveLegal method
        var moveGeneratorType = typeof(MoveGenerator);
        var isMoveLegalMethod = moveGeneratorType.GetMethod("IsMoveLegal", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(isMoveLegalMethod, "Could not find IsMoveLegal method");
        
        // Also get the field to set the position
        var positionField = moveGeneratorType.GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(positionField, "Could not find _position field");
        positionField.SetValue(_moveGenerator, position);
        
        foreach (var (fromStr, toStr, desc) in testMoves)
        {
            var from = SquareExtensions.ParseSquare(fromStr);
            var to = SquareExtensions.ParseSquare(toStr);
            var piece = position.GetPiece(from);
            var captured = position.GetPiece(to);
            
            // Create the move
            var flags = MoveType.None;
            if (captured != Piece.None) flags |= MoveType.Capture;
            if (fromStr == "e5" && toStr == "f6") flags |= MoveType.EnPassant | MoveType.Capture;
            
            var move = new Move(from, to, flags, captured);
            
            // Test if the move is considered legal
            var isLegal = (bool)isMoveLegalMethod!.Invoke(_moveGenerator, new object[] { move })!;
            
            Console.WriteLine($"{desc}: {(isLegal ? "LEGAL" : "ILLEGAL")}");
            
            if (!isLegal)
            {
                // Try to understand why it's illegal
                var undoInfo = position.MakeMove(move);
                
                // Check where our king is
                var ourKing = position.GetBitboard(Color.White, PieceType.King);
                if (ourKing.IsEmpty())
                {
                    Console.WriteLine($"  King was captured!");
                }
                else
                {
                    var kingSquare = (Square)ourKing.GetLsbIndex();
                    var isKingAttacked = MoveGenerator.IsSquareAttacked(position, kingSquare, Color.Black);
                    Console.WriteLine($"  King on {kingSquare.ToAlgebraic()}, attacked: {isKingAttacked}");
                    
                    if (isKingAttacked)
                    {
                        // Find what's attacking the king
                        var attackers = MoveGenerator.GetAttackers(position, kingSquare, position.OccupiedSquares()) & position.GetBitboard(Color.Black);
                        Console.WriteLine($"  Attackers: {GetSquareList(attackers)}");
                    }
                }
                
                position.UnmakeMove(move, undoInfo);
            }
        }
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