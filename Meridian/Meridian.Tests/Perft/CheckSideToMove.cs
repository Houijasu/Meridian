#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class CheckSideToMove
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void VerifySideToMoveAfterH2H4()
    {
        // Starting position
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("Initial position:");
        Console.WriteLine($"Side to move: {position.SideToMove}");
        Console.WriteLine();

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

        Assert.IsNotNull(h2h4Move);
        
        // Make the move
        var undoInfo = position.MakeMove(h2h4Move.Value);
        
        Console.WriteLine("After h2h4:");
        Console.WriteLine($"Side to move: {position.SideToMove}");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine();
        
        // Generate moves for Black
        var blackMoves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref blackMoves);
        
        Console.WriteLine($"Number of moves generated: {blackMoves.Count}");
        Console.WriteLine("First 10 moves:");
        for (int i = 0; i < Math.Min(10, blackMoves.Count); i++)
        {
            var move = blackMoves[i];
            var from = move.From;
            var to = move.To;
            var piece = position.GetPiece(from);
            Console.WriteLine($"  {move.ToUci()} - Piece at {from}: {piece}");
        }
        
        // Check if any of these are actually White moves
        bool foundWhiteMove = false;
        for (int i = 0; i < blackMoves.Count; i++)
        {
            var move = blackMoves[i];
            var piece = position.GetPiece(move.From);
            if (piece != Piece.None && piece.GetColor() == Color.White)
            {
                foundWhiteMove = true;
                Console.WriteLine($"ERROR: Found White move: {move.ToUci()} (piece: {piece})");
            }
        }
        
        Assert.IsFalse(foundWhiteMove, "Move generator is generating White moves when it should generate Black moves!");
        Assert.AreEqual(Color.Black, position.SideToMove, "Side to move should be Black after White's h2h4");
    }
}