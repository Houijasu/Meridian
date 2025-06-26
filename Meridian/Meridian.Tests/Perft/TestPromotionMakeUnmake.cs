#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestPromotionMakeUnmake
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void VerifyPromotionMakeUnmake()
    {
        // Simple position with one pawn ready to promote
        var positionResult = Position.FromFen("8/P6k/8/8/8/8/7K/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Initial position: {position.ToFen()}");
        Console.WriteLine($"White pawn on a7, ready to promote");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        // Find promotion moves
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if ((move.Flags & MoveType.Promotion) != 0)
            {
                Console.WriteLine($"\nTesting promotion: {move.ToUci()} to {move.PromotionType}");
                
                // Save initial FEN
                var initialFen = position.ToFen();
                
                // Make the move
                var undoInfo = position.MakeMove(move);
                var afterMoveFen = position.ToFen();
                Console.WriteLine($"After move: {afterMoveFen}");
                
                // Check the promoted piece
                var promotedPiece = position.GetPiece(move.To);
                Console.WriteLine($"Piece on {move.To}: {promotedPiece} (should be White {move.PromotionType})");
                Assert.AreEqual(PieceExtensions.MakePiece(Color.White, move.PromotionType), promotedPiece);
                
                // Unmake the move
                position.UnmakeMove(move, undoInfo);
                var afterUnmakeFen = position.ToFen();
                Console.WriteLine($"After unmake: {afterUnmakeFen}");
                
                // Verify we're back to the initial position
                Assert.AreEqual(initialFen, afterUnmakeFen, "Position not restored correctly after unmake");
                
                // Verify the pawn is back
                var pawn = position.GetPiece(Square.A7);
                Assert.AreEqual(Piece.WhitePawn, pawn, "Pawn not restored after unmake");
            }
        }
        
        // Now test a more complex scenario - make multiple moves and unmake
        Console.WriteLine("\n\nTesting sequence of moves with promotion:");
        
        // Make king move first
        var kingMove = new Move(Square.H2, Square.H3);
        var undoInfo1 = position.MakeMove(kingMove);
        Console.WriteLine($"After Kh3: {position.ToFen()}");
        
        // Black king moves
        var blackKingMove = new Move(Square.H7, Square.H6);
        var undoInfo2 = position.MakeMove(blackKingMove);
        Console.WriteLine($"After Kh6: {position.ToFen()}");
        
        // White promotes
        var promoteMove = new Move(Square.A7, Square.A8, MoveType.Promotion, Piece.None, PieceType.Queen);
        var undoInfo3 = position.MakeMove(promoteMove);
        Console.WriteLine($"After a8=Q: {position.ToFen()}");
        
        // Unmake in reverse order
        position.UnmakeMove(promoteMove, undoInfo3);
        Console.WriteLine($"Unmake a8=Q: {position.ToFen()}");
        
        position.UnmakeMove(blackKingMove, undoInfo2);
        Console.WriteLine($"Unmake Kh6: {position.ToFen()}");
        
        position.UnmakeMove(kingMove, undoInfo1);
        Console.WriteLine($"Unmake Kh3: {position.ToFen()}");
        
        Assert.AreEqual("8/P6k/8/8/8/8/7K/8 w - - 0 1", position.ToFen());
    }
}