#nullable enable

using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Meridian.Tests.Board;

[TestClass]
public sealed class MakeUnmakeTests
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestMakeUnmakeRestoresPosition()
    {
        var positions = new[]
        {
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8"
        };
        
        Span<Move> moveBuffer = stackalloc Move[218];
        
        foreach (var fen in positions)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess, $"Failed to parse FEN: {fen}");
            var position = positionResult.Value;
            
            // Save original state
            var originalFen = position.ToFen();
            var originalZobrist = position.ZobristKey;
            var originalSideToMove = position.SideToMove;
            var originalCastlingRights = position.CastlingRights;
            var originalEnPassant = position.EnPassantSquare;
            var originalHalfmove = position.HalfmoveClock;
            
            // Generate all legal moves
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            // Test each move
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                
                // Make the move
                var undoInfo = position.MakeMove(move);
                
                // Unmake the move
                position.UnmakeMove(move, undoInfo);
                
                // Verify position is restored
                Assert.AreEqual(originalFen, position.ToFen(), 
                    $"Position not restored after move {move.ToUci()} in position {fen}");
                Assert.AreEqual(originalZobrist, position.ZobristKey,
                    $"Zobrist key not restored after move {move.ToUci()} in position {fen}");
                Assert.AreEqual(originalSideToMove, position.SideToMove,
                    $"Side to move not restored after move {move.ToUci()} in position {fen}");
                Assert.AreEqual(originalCastlingRights, position.CastlingRights,
                    $"Castling rights not restored after move {move.ToUci()} in position {fen}");
                Assert.AreEqual(originalEnPassant, position.EnPassantSquare,
                    $"En passant square not restored after move {move.ToUci()} in position {fen}");
                Assert.AreEqual(originalHalfmove, position.HalfmoveClock,
                    $"Halfmove clock not restored after move {move.ToUci()} in position {fen}");
            }
        }
    }
    
    [TestMethod]
    public void TestMakeUnmakeSpecialMoves()
    {
        // Test castling
        var castlingResult = Position.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        Assert.IsTrue(castlingResult.IsSuccess);
        var position = castlingResult.Value;
        
        var castlingMoves = new[] 
        { 
            new Move(Square.E1, Square.G1, MoveType.Castling),
            new Move(Square.E1, Square.C1, MoveType.Castling) 
        };
        
        foreach (var move in castlingMoves)
        {
            var originalFen = position.ToFen();
            var undoInfo = position.MakeMove(move);
            position.UnmakeMove(move, undoInfo);
            Assert.AreEqual(originalFen, position.ToFen(), $"Castling move {move.ToUci()} not properly undone");
        }
        
        // Test en passant
        var enPassantResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3");
        Assert.IsTrue(enPassantResult.IsSuccess);
        position = enPassantResult.Value;
        
        var epMove = new Move(Square.E5, Square.F6, MoveType.EnPassant | MoveType.Capture, 
                              PieceExtensions.MakePiece(Color.Black, PieceType.Pawn));
        var originalFen2 = position.ToFen();
        var undoInfo2 = position.MakeMove(epMove);
        position.UnmakeMove(epMove, undoInfo2);
        Assert.AreEqual(originalFen2, position.ToFen(), "En passant move not properly undone");
        
        // Test promotion
        var promotionResult = Position.FromFen("rnbqkbnr/pppppP1p/8/8/8/8/PPPPP1PP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(promotionResult.IsSuccess);
        position = promotionResult.Value;
        
        var promoMove = new Move(Square.F7, Square.F8, MoveType.Promotion, Piece.None, PieceType.Queen);
        var originalFen3 = position.ToFen();
        var undoInfo3 = position.MakeMove(promoMove);
        position.UnmakeMove(promoMove, undoInfo3);
        Assert.AreEqual(originalFen3, position.ToFen(), "Promotion move not properly undone");
    }
    
}