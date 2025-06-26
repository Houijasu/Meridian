#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugBishopMove
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugF1B5Move()
    {
        // Position: rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Find the f1b5 move
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Move? f1b5Move = null;
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.ToUci() == "f1b5")
            {
                f1b5Move = move;
                break;
            }
        }
        
        if (!f1b5Move.HasValue)
        {
            Assert.Fail("f1b5 move not found!");
            return;
        }
        
        Console.WriteLine("Found f1b5 move");
        Console.WriteLine($"Move flags: {f1b5Move.Value.Flags}");
        
        // Make the move
        var undoInfo = position.MakeMove(f1b5Move.Value);
        
        Console.WriteLine($"\nPosition after f1b5:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        
        // Check if Black king is in check
        var blackKing = position.GetBitboard(Color.Black, PieceType.King);
        if (blackKing.IsNotEmpty())
        {
            var kingSquare = (Square)blackKing.GetLsbIndex();
            var isInCheck = MoveGenerator.IsSquareAttacked(position, kingSquare, Color.White);
            Console.WriteLine($"Black king on {kingSquare}: {(isInCheck ? "IN CHECK" : "not in check")}");
        }
        
        // Generate Black's moves
        moves.Clear();
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nBlack has {moves.Count} moves after f1b5");
        Console.WriteLine("Expected: 6 moves (Stockfish)");
        
        if (moves.Count < 6)
        {
            // List all moves
            Console.WriteLine("\nMoves found:");
            for (int i = 0; i < moves.Count; i++)
            {
                Console.WriteLine($"  {moves[i].ToUci()}");
            }
            
            // Check what pieces can move
            var pieces = new[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, 
                                PieceType.Rook, PieceType.Queen, PieceType.King };
            
            Console.WriteLine("\nPieces that can move:");
            foreach (var pieceType in pieces)
            {
                var pieceBB = position.GetBitboard(Color.Black, pieceType);
                if (pieceBB.IsNotEmpty())
                {
                    var count = Bitboard.PopCount(pieceBB);
                    Console.WriteLine($"  {pieceType}: {count} piece(s)");
                }
            }
        }
        
        position.UnmakeMove(f1b5Move.Value, undoInfo);
        
        // Write output to file
        var output = new System.Text.StringBuilder();
        output.AppendLine($"Moves after f1b5: {moves.Count} (expected 6)");
        for (int i = 0; i < moves.Count; i++)
        {
            output.AppendLine(moves[i].ToUci());
        }
        System.IO.File.WriteAllText("/tmp/f1b5_debug.txt", output.ToString());
        
        if (moves.Count != 6)
        {
            Assert.Fail($"Expected 6 moves after f1b5, got {moves.Count}");
        }
    }
}