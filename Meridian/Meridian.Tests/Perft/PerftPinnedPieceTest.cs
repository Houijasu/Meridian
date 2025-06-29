#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftPinnedPieceTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestPinnedPieceGeneration()
    {
        // Position with pinned pieces
        var tests = new[]
        {
            // Bishop pinned by queen
            ("r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 1", 1),
            // Knight pinned by rook
            ("rnbqkb1r/pppp1ppp/5n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 1", 1),
            // Pawn pinned diagonally
            ("r1bqkbnr/ppp1pppp/2n5/3p4/3PP3/5N2/PPP2PPP/RNBQKB1R w KQkq - 0 1", 1),
            // Multiple pins
            ("r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQK2R w KQkq - 0 1", 1)
        };
        
        Span<Move> moveBuffer = stackalloc Move[218];
        
        foreach (var (fen, depth) in tests)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            
            var position = positionResult.Value;
            
            // Generate moves
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            // Make each move and verify king is not in check
            var invalidMoves = new List<string>();
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                var undoInfo = position.MakeMove(move);
                
                // Check if our king is under attack
                var kingColor = position.SideToMove == Color.White ? Color.Black : Color.White;
                var king = position.GetBitboard(kingColor, PieceType.King);
                if (king.IsNotEmpty())
                {
                    var kingSquare = (Square)king.GetLsbIndex();
                    if (MoveGenerator.IsSquareAttacked(position, kingSquare, position.SideToMove))
                    {
                        invalidMoves.Add($"{move.ToUci()} leaves king in check");
                    }
                }
                
                position.UnmakeMove(move, undoInfo);
            }
            
            if (invalidMoves.Count > 0)
            {
                Assert.Fail($"Invalid moves found in position {fen}:\n{string.Join("\n", invalidMoves)}");
            }
        }
    }
    
    [TestMethod]
    public void TestEnPassantPinned()
    {
        // Special case: en passant capture that would expose king to check
        // Position where en passant would expose king to horizontal rook attack
        var fenWithRook = "8/8/8/8/k2pP2r/8/8/4K3 w - d6 0 1";
        var positionResult = Position.FromFen(fenWithRook);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        // Check if en passant is generated
        var enPassantMoves = new List<string>();
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if ((move.Flags & MoveType.EnPassant) != MoveType.None)
            {
                enPassantMoves.Add(move.ToUci());
            }
        }
        
        // En passant should NOT be generated as it would expose king
        Assert.AreEqual(0, enPassantMoves.Count, 
            $"En passant moves should not be generated when they expose king. Found: {string.Join(", ", enPassantMoves)}");
    }
}