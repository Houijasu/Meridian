#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class EnPassantDeepDive
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestEnPassantCapture()
    {
        var positionResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("Initial position:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine($"En passant square: {position.EnPassantSquare}");
        
        // Find the en passant move
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Move? epMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == "e5f6")
            {
                epMove = moves[i];
                break;
            }
        }
        
        Assert.IsTrue(epMove.HasValue, "En passant move e5f6 not found!");
        
        // Make the en passant capture
        Console.WriteLine("\nMaking en passant capture e5f6...");
        var undoInfo = position.MakeMove(epMove.Value);
        
        Console.WriteLine("Position after en passant:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        
        // Check if the captured pawn was removed
        var f5Square = Square.F5;
        var pieceOnF5 = position.GetPiece(f5Square);
        Console.WriteLine($"Piece on f5 (should be None): {pieceOnF5}");
        
        // Check if the capturing pawn is on f6
        var f6Square = Square.F6;
        var pieceOnF6 = position.GetPiece(f6Square);
        Console.WriteLine($"Piece on f6 (should be white pawn): {pieceOnF6}");
        
        // Generate moves after en passant
        Span<Move> movesAfter = stackalloc Move[218];
        var movesListAfter = new MoveList(movesAfter);
        _moveGenerator.GenerateMoves(position, ref movesListAfter);
        
        Console.WriteLine($"\nMoves after en passant: {movesListAfter.Count}");
        
        // Test perft after en passant
        var nodesDepth1 = Perft(position, 1);
        var nodesDepth2 = Perft(position, 2);
        
        Console.WriteLine($"Perft(1) after en passant: {nodesDepth1}");
        Console.WriteLine($"Perft(2) after en passant: {nodesDepth2}");
        
        // Unmake and verify
        position.UnmakeMove(epMove.Value, undoInfo);
        Console.WriteLine($"\nPosition after unmake:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine($"En passant square restored: {position.EnPassantSquare}");
        
        // Verify pieces are restored
        pieceOnF5 = position.GetPiece(f5Square);
        Console.WriteLine($"Piece on f5 after unmake (should be black pawn): {pieceOnF5}");
        pieceOnF6 = position.GetPiece(f6Square);
        Console.WriteLine($"Piece on f6 after unmake (should be None): {pieceOnF6}");
    }
    
    private ulong Perft(Position position, int depth)
    {
        if (depth == 0) return 1;
        
        ulong nodes = 0;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        for (int i = 0; i < moves.Count; i++)
        {
            var undoInfo = position.MakeMove(moves[i]);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(moves[i], undoInfo);
        }
        
        return nodes;
    }
}