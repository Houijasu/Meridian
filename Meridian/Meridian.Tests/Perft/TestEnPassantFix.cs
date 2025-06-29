#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class TestEnPassantFix
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void VerifyEnPassantNotSetAfterH2H4()
    {
        // Starting position
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        // Generate moves and find h2h4
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        Move? h2h4Move = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == "h2h4")
            {
                h2h4Move = moves[i];
                break;
            }
        }

        Assert.IsNotNull(h2h4Move, "h2h4 move not found!");
        
        // Make the move
        var undoInfo = position.MakeMove(h2h4Move.Value);
        
        // Check en passant square
        Console.WriteLine($"Position after h2h4:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine($"En passant square: {(position.EnPassantSquare != Square.None ? position.EnPassantSquare.ToString() : "None")}");
        
        // Assert en passant is None
        Assert.AreEqual(Square.None, position.EnPassantSquare, 
            "En passant square should be None after h2h4 (no black pawns can capture)");
    }

    [TestMethod]
    public void VerifyEnPassantSetWhenCanBeCaptured()
    {
        // Position where white plays d2d4 and black has a pawn on c4
        var positionResult = Position.FromFen("rnbqkbnr/pp1ppppp/8/8/2p5/8/PPPPPPPP/RNBQKBNR w KQkq - 0 2");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        // Generate moves and find d2d4
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        Move? d2d4Move = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == "d2d4")
            {
                d2d4Move = moves[i];
                break;
            }
        }

        Assert.IsNotNull(d2d4Move, "d2d4 move not found!");
        
        // Make the move
        var undoInfo = position.MakeMove(d2d4Move.Value);
        
        // Check en passant square
        Console.WriteLine($"Position after d2d4 (with black pawn on c4):");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine($"En passant square: {(position.EnPassantSquare != Square.None ? position.EnPassantSquare.ToString() : "None")}");
        
        // Assert en passant is d3
        Assert.AreEqual(Square.D3, position.EnPassantSquare, 
            "En passant square should be D3 after d2d4 (black pawn on c4 can capture)");
    }
}