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
public class AnalyzeH2H4Move
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void AnalyzeH2H4Discrepancy()
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

        Assert.IsNotNull(h2h4Move, "h2h4 move not found!");
        
        var undoInfo = position.MakeMove(h2h4Move.Value);
        
        // Position after h2h4
        Console.WriteLine("Position after 1.h4:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine();

        // Expected perft(3) = 9329 (from Stockfish)
        // Our perft(3) = 9329 + 37 = 9366
        var ourNodes = Perft(position, 3);
        Console.WriteLine($"Our perft(3) after h2h4: {ourNodes}");
        Console.WriteLine($"Expected (Stockfish): 9329");
        Console.WriteLine($"Difference: +{ourNodes - 9329}");
        Console.WriteLine();

        // Now let's do perft divide at depth 2 to find which response is wrong
        Console.WriteLine("Perft divide at depth 2 after h2h4:");
        
        // Stockfish depth 3 divide after h2h4 (need to get this data)
        // For now, let's just show our results
        var divideResults = new Dictionary<string, ulong>();
        var blackMoves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref blackMoves);

        for (int i = 0; i < blackMoves.Count; i++)
        {
            var move = blackMoves[i];
            var moveUndoInfo = position.MakeMove(move);
            var nodes = Perft(position, 2);
            position.UnmakeMove(move, moveUndoInfo);
            divideResults[move.ToUci()] = nodes;
        }

        // Show all moves sorted by node count
        foreach (var (moveStr, nodes) in divideResults.OrderByDescending(r => r.Value))
        {
            Console.WriteLine($"  {moveStr}: {nodes}");
        }

        var total = divideResults.Values.Aggregate(0UL, (sum, val) => sum + val);
        Console.WriteLine($"\nTotal: {total} (should be 9329)");

        // Check for special cases that might cause issues
        Console.WriteLine("\nSpecial cases to check:");
        Console.WriteLine("- En passant: " + (position.EnPassantSquare != Square.None ? position.EnPassantSquare.ToString() : "None"));
        Console.WriteLine("- Castling rights: " + position.CastlingRights);
        
        position.UnmakeMove(h2h4Move.Value, undoInfo);
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