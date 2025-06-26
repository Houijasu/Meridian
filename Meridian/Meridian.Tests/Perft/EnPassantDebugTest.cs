#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class EnPassantDebugTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugEnPassantPerft()
    {
        // Position with en passant: after 1.e4 e6 2.e5 f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Total moves from position: {moves.Count}");
        
        // Find en passant move
        Move? enPassantMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if ((move.Flags & MoveType.EnPassant) != MoveType.None)
            {
                enPassantMove = move;
                Console.WriteLine($"En passant move found: {move.ToUci()}");
            }
        }
        
        if (enPassantMove.HasValue)
        {
            // Make the en passant capture
            var undoInfo = position.MakeMove(enPassantMove.Value);
            
            Console.WriteLine($"\nPosition after en passant:");
            Console.WriteLine($"FEN: {position.ToFen()}");
            
            // Count moves after en passant
            moves.Clear();
            _moveGenerator.GenerateMoves(position, ref moves);
            Console.WriteLine($"Moves after en passant: {moves.Count}");
            
            // Verify the captured pawn is gone
            var f5Square = Square.F5;
            var pieceAtF5 = position.GetPiece(f5Square);
            Console.WriteLine($"Piece at f5: {pieceAtF5}");
            
            position.UnmakeMove(enPassantMove.Value, undoInfo);
        }
        
        // Now let's do perft(1) and perft(2) and compare with Stockfish
        var perft1 = Perft(position, 1);
        var perft2 = Perft(position, 2);
        
        Console.WriteLine($"\nPerft(1): {perft1} (expected 31)");
        Console.WriteLine($"Perft(2): {perft2} (expected 707)");
        
        if (perft2 != 707)
        {
            Assert.Fail($"Perft(2) mismatch: got {perft2}, expected 707");
        }
        
        // Do perft divide at depth 2 to see which moves are wrong
        Console.WriteLine("\nPerft divide depth 2:");
        PerftDivide(position, 2);
        
        // Save to file for easier comparison
        var output = new System.Text.StringBuilder();
        output.AppendLine($"Perft(1): {perft1}");
        output.AppendLine($"Perft(2): {perft2}");
        System.IO.File.WriteAllText("/tmp/our_perft.txt", output.ToString());
    }
    
    private void PerftDivide(Position position, int depth)
    {
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        var results = new System.Collections.Generic.SortedDictionary<string, ulong>();
        ulong total = 0;
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            var count = depth > 1 ? Perft(position, depth - 1) : 1;
            position.UnmakeMove(move, undoInfo);
            
            results[move.ToUci()] = count;
            total += count;
        }
        
        // Write to file for comparison
        var output = new System.Text.StringBuilder();
        foreach (var kvp in results)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            output.AppendLine($"{kvp.Key}: {kvp.Value}");
        }
        
        Console.WriteLine($"Total: {total}");
        output.AppendLine($"Total: {total}");
        System.IO.File.WriteAllText("/tmp/our_perft_divide.txt", output.ToString());
    }
    
    private ulong Perft(Position position, int depth)
    {
        if (depth == 0) return 1;

        ulong nodes = 0;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(move, undoInfo);
        }

        return nodes;
    }
}