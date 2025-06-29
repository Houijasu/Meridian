#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugZeroMoves
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void DebugBlackCastlingZeroMoves()
    {
        // This position generates 0 moves but should generate 16
        var fen = "8/8/8/8/8/8/8/R3K2r b Qk - 0 1";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {fen}");
        Console.WriteLine($"Side to move: {position.SideToMove}");
        Console.WriteLine($"Castling rights: {position.CastlingRights}");
        Console.WriteLine();
        
        // Check piece locations
        Console.WriteLine("Piece locations:");
        Console.WriteLine($"White rook on a1: {position.GetPiece(Square.A1)}");
        Console.WriteLine($"White king on e1: {position.GetPiece(Square.E1)}");
        Console.WriteLine($"Black rook on h1: {position.GetPiece(Square.H1)}");
        Console.WriteLine($"Black king location: ???");
        
        // Find black king
        for (int sq = 0; sq < 64; sq++)
        {
            var piece = position.GetPiece((Square)sq);
            if (piece == Piece.BlackKing)
            {
                Console.WriteLine($"Black king found on: {((Square)sq).ToAlgebraic()}");
            }
        }
        
        // Check bitboards
        var blackPieces = position.GetBitboard(Color.Black);
        var blackKings = position.GetBitboard(Color.Black, PieceType.King);
        var blackRooks = position.GetBitboard(Color.Black, PieceType.Rook);
        
        Console.WriteLine($"\nBlack pieces bitboard: {blackPieces.Value:X16}");
        Console.WriteLine($"Black kings bitboard: {blackKings.Value:X16}");
        Console.WriteLine($"Black rooks bitboard: {blackRooks.Value:X16}");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nGenerated {moves.Count} moves (expected 16)");
        
        // Try to generate moves manually for black rook on h1
        Console.WriteLine("\nManually checking black rook on h1:");
        var rookSquare = Square.H1;
        var rook = position.GetPiece(rookSquare);
        Console.WriteLine($"Piece on h1: {rook}");
        
        // Check if it's actually a black rook
        if (rook == Piece.BlackRook)
        {
            Console.WriteLine("Confirmed: Black rook on h1");
            
            // Check possible moves
            var possibleSquares = new[] { "g1", "f1", "e1", "d1", "c1", "b1", "a1", "h2", "h3", "h4", "h5", "h6", "h7", "h8" };
            foreach (var sq in possibleSquares)
            {
                var target = SquareExtensions.ParseSquare(sq);
                var targetPiece = position.GetPiece(target);
                Console.WriteLine($"  h1-{sq}: {(targetPiece == Piece.None ? "empty" : targetPiece.ToString())}");
            }
        }
        
        // Let's also check the FEN parsing
        Console.WriteLine("\nRe-parsing FEN to verify:");
        var parts = fen.Split(' ');
        Console.WriteLine($"Board part: {parts[0]}");
        Console.WriteLine($"Side to move: {parts[1]}");
        Console.WriteLine($"Castling: {parts[2]}");
    }
    
    [TestMethod]
    public void TestCorrectFENForBlackMoves()
    {
        // Let's test a simpler position with just black rook
        var fen = "8/8/8/8/8/8/8/7r b - - 0 1";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"\nTesting simpler position: {fen}");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Generated {moves.Count} moves for black rook on h1");
        for (int i = 0; i < moves.Count; i++)
        {
            Console.WriteLine($"  {i+1}. {moves[i].ToUci()}");
        }
    }
}