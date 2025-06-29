#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class ValidateFailingFENs
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void ValidateProblematicFENs()
    {
        // These FENs appear to be invalid or have issues
        var problematicFens = new[]
        {
            ("8/8/8/8/8/8/8/R3K2r b Qk - 0 1", "Missing black king"),
            ("8/8/8/8/8/8/8/r3k2R b Kq - 0 1", "Might be missing pieces"),
            ("K7/8/8/3p4/4p3/8/8/7k b - - 0 1", "Looks valid"),
            ("k7/8/8/8/3P4/8/8/K7 w - - 0 1", "Looks valid"),
            ("8/8/8/8/k2Pp2K/8/8/8 b - d3 0 1", "Looks valid")
        };
        
        Span<Move> moveBuffer = stackalloc Move[218];

        foreach (var (fen, note) in problematicFens)
        {
            Console.WriteLine($"\nFEN: {fen}");
            Console.WriteLine($"Note: {note}");
            
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine("Failed to parse FEN!");
                continue;
            }
            
            var position = positionResult.Value;
            
            // Count pieces
            var whitePieceCount = 0;
            var blackPieceCount = 0;
            var whiteKingFound = false;
            var blackKingFound = false;
            
            for (int sq = 0; sq < 64; sq++)
            {
                var piece = position.GetPiece((Square)sq);
                if (piece != Piece.None)
                {
                    if (piece.GetColor() == Color.White)
                    {
                        whitePieceCount++;
                        if (piece == Piece.WhiteKing) whiteKingFound = true;
                    }
                    else
                    {
                        blackPieceCount++;
                        if (piece == Piece.BlackKing) blackKingFound = true;
                    }
                    Console.WriteLine($"  {((Square)sq).ToAlgebraic()}: {piece}");
                }
            }
            
            Console.WriteLine($"White pieces: {whitePieceCount} (King: {whiteKingFound})");
            Console.WriteLine($"Black pieces: {blackPieceCount} (King: {blackKingFound})");
            
            if (!whiteKingFound || !blackKingFound)
            {
                Console.WriteLine("WARNING: Missing king! This is an invalid position.");
            }
            
            // Generate moves
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            Console.WriteLine($"Generated moves: {moves.Count}");
        }
        
        // Now let's test the correct versions of these positions
        Console.WriteLine("\n\n=== CORRECTED POSITIONS ===");
        
        var correctedFens = new[]
        {
            ("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "Both kings and rooks present"),
            ("r3k3/8/8/8/8/8/8/R3K2R b KQ - 0 1", "Black king on e8, no black kingside castling"),
            ("4k3/8/8/8/8/8/8/R3K2r b Qk - 0 1", "Black king on e8")
        };
        
        Span<Move> moveBuffer2 = stackalloc Move[218];
        
        foreach (var (fen, note) in correctedFens)
        {
            Console.WriteLine($"\nFEN: {fen}");
            Console.WriteLine($"Note: {note}");
            
            var positionResult = Position.FromFen(fen);
            if (positionResult.IsFailure)
            {
                Console.WriteLine("Failed to parse FEN!");
                continue;
            }
            
            var position = positionResult.Value;
            
            // Generate moves
            var moves = new MoveList(moveBuffer2);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            Console.WriteLine($"Generated moves: {moves.Count}");
            
            // Show some moves
            Console.WriteLine("Sample moves:");
            for (int i = 0; i < Math.Min(5, moves.Count); i++)
            {
                Console.WriteLine($"  {moves[i].ToUci()}");
            }
        }
    }
}