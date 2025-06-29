#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class VerifyAllPawnPushesEnPassant
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestAllPawnPushesFromStartingPosition()
    {
        // Starting position
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        // Test all double pawn pushes
        var doublePushMoves = new[] { "a2a4", "b2b4", "c2c4", "d2d4", "e2e4", "f2f4", "g2g4", "h2h4" };
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        Console.WriteLine("Testing all double pawn pushes from starting position:");
        Console.WriteLine("(En passant should always be None since no black pawns can capture)\n");
        
        foreach (var moveStr in doublePushMoves)
        {
            // Find the move
            Move? targetMove = null;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].ToUci() == moveStr)
                {
                    targetMove = moves[i];
                    break;
                }
            }
            
            Assert.IsNotNull(targetMove, $"Move {moveStr} not found!");
            
            // Make the move
            var undoInfo = position.MakeMove(targetMove.Value);
            
            Console.WriteLine($"After {moveStr}:");
            Console.WriteLine($"  FEN: {position.ToFen()}");
            Console.WriteLine($"  En passant: {(position.EnPassantSquare != Square.None ? position.EnPassantSquare.ToString() : "None")}");
            
            Assert.AreEqual(Square.None, position.EnPassantSquare, 
                $"En passant should be None after {moveStr} from starting position");
            
            // Unmake the move
            position.UnmakeMove(targetMove.Value, undoInfo);
        }
        
        Console.WriteLine("\nAll tests passed!");
    }
    
    [TestMethod]
    public void TestEnPassantWithAdjacentPawns()
    {
        // Test positions where en passant SHOULD be set
        var testCases = new[]
        {
            // White d2d4 with black pawn on c4
            ("rnbqkbnr/pp1ppppp/8/8/2p5/8/PPPPPPPP/RNBQKBNR w KQkq - 0 2", "d2d4", Square.D3),
            // White d2d4 with black pawn on e4
            ("rnbqkbnr/pp1ppppp/8/8/4p3/8/PPPPPPPP/RNBQKBNR w KQkq - 0 2", "d2d4", Square.D3),
            // White a2a4 with black pawn on b4
            ("rnbqkbnr/p1pppppp/8/8/1p6/8/PPPPPPPP/RNBQKBNR w KQkq - 0 2", "a2a4", Square.A3),
            // White h2h4 with black pawn on g4
            ("rnbqkbnr/pppppp1p/8/8/6p1/8/PPPPPPPP/RNBQKBNR w KQkq - 0 2", "h2h4", Square.H3),
        };
        
        Console.WriteLine("\nTesting en passant with adjacent pawns:");
        
        Span<Move> moveBuffer = stackalloc Move[218];
        
        foreach (var (fen, moveStr, expectedEp) in testCases)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            var position = positionResult.Value;
            
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            // Find the move
            Move? targetMove = null;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].ToUci() == moveStr)
                {
                    targetMove = moves[i];
                    break;
                }
            }
            
            Assert.IsNotNull(targetMove, $"Move {moveStr} not found!");
            
            // Make the move
            var undoInfo = position.MakeMove(targetMove.Value);
            
            Console.WriteLine($"\nFEN: {fen}");
            Console.WriteLine($"Move: {moveStr}");
            Console.WriteLine($"Expected EP: {expectedEp}");
            Console.WriteLine($"Actual EP: {position.EnPassantSquare}");
            Console.WriteLine($"Result FEN: {position.ToFen()}");
            
            Assert.AreEqual(expectedEp, position.EnPassantSquare, 
                $"En passant should be {expectedEp} after {moveStr}");
            
            position.UnmakeMove(targetMove.Value, undoInfo);
        }
    }
}