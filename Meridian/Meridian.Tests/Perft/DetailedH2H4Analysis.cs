#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class DetailedH2H4Analysis
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void AnalyzeH2H4AtDepth2()
    {
        // Starting position
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        // Make h2h4 move
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
        var undoInfo = position.MakeMove(h2h4Move.Value);

        Console.WriteLine("Position after 1.h4:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine($"En passant: {(position.EnPassantSquare != Square.None ? position.EnPassantSquare.ToString() : "None")}");
        Console.WriteLine();

        // Stockfish perft(3) after h2h4 = 9329
        // Let's do a perft divide at depth 2 to find discrepancies
        var stockfishDepth2AfterH4 = new Dictionary<string, ulong>
        {
            // These are made up - we need the actual Stockfish values
            // But first let's see what our engine generates
        };

        var ourDivide = new Dictionary<string, ulong>();
        var blackMoves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref blackMoves);

        Console.WriteLine($"Black has {blackMoves.Count} legal moves after h4");
        
        for (int i = 0; i < blackMoves.Count; i++)
        {
            var move = blackMoves[i];
            var moveUndoInfo = position.MakeMove(move);
            
            // Count responses at depth 1
            var whiteMoves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref whiteMoves);
            ourDivide[move.ToUci()] = (ulong)whiteMoves.Count;
            
            position.UnmakeMove(move, moveUndoInfo);
        }

        // Show all black's responses sorted by node count
        Console.WriteLine("\nBlack's responses (depth 1 node count):");
        foreach (var (moveStr, nodes) in ourDivide.OrderByDescending(r => r.Value))
        {
            Console.WriteLine($"  {moveStr}: {nodes}");
        }

        var total = ourDivide.Values.Aggregate(0UL, (sum, val) => sum + val);
        Console.WriteLine($"\nTotal at depth 1: {total}");
        Console.WriteLine($"Expected total at depth 2: 420 (20 moves × 21 responses)");
        
        // Now let's check some specific moves that might be problematic
        Console.WriteLine("\nChecking specific black moves:");
        
        // Check if any pawn moves are creating invalid en passant squares
        foreach (var moveStr in new[] { "a7a5", "b7b5", "c7c5", "d7d5", "e7e5", "f7f5", "g7g5", "h7h5" })
        {
            if (ourDivide.ContainsKey(moveStr))
            {
                // Make the move and check en passant
                for (int i = 0; i < blackMoves.Count; i++)
                {
                    if (blackMoves[i].ToUci() == moveStr)
                    {
                        var move = blackMoves[i];
                        var moveUndoInfo = position.MakeMove(move);
                        Console.WriteLine($"\nAfter {moveStr}:");
                        Console.WriteLine($"  FEN: {position.ToFen()}");
                        Console.WriteLine($"  En passant: {(position.EnPassantSquare != Square.None ? position.EnPassantSquare.ToString() : "None")}");
                        position.UnmakeMove(move, moveUndoInfo);
                        break;
                    }
                }
            }
        }
        
        position.UnmakeMove(h2h4Move.Value, undoInfo);
        
        // Force failure to see output
        Assert.Fail("Forcing output display");
    }
}