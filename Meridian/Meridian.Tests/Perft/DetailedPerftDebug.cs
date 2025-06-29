#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;

namespace Meridian.Tests.Perft;

[TestClass]
public class DetailedPerftDebug
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void DebugStartingPositionDepth2()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("Starting position perft(2) - checking each first move:");
        Console.WriteLine("Expected total: 400");
        
        var total = 0UL;
        var differences = new List<string>();
        
        // Expected perft(1) after each move (all should be 20)
        var expectedPerMove = 20UL;
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Span<Move> responseBuffer = stackalloc Move[218];
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            
            // Count responses
            var responses = new MoveList(responseBuffer);
            _moveGenerator.GenerateMoves(position, ref responses);
            
            var count = (ulong)responses.Count;
            total += count;
            
            if (count != expectedPerMove)
            {
                var diff = $"{move.ToUci()}: expected {expectedPerMove}, got {count} (diff: {(long)count - (long)expectedPerMove})";
                differences.Add(diff);
                Console.WriteLine(diff);
            }
            
            position.UnmakeMove(move, undoInfo);
        }
        
        Console.WriteLine($"\nTotal: {total} (expected: 400)");
        
        if (differences.Count > 0)
        {
            Assert.Fail($"Found {differences.Count} moves with incorrect response counts");
        }
    }
    
    [TestMethod]
    public void DebugH2H4Specifically()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        // Make h2h4
        var h2h4 = new Move(Square.H2, Square.H4, MoveType.DoublePush);
        var undoInfo = position.MakeMove(h2h4);
        
        Console.WriteLine($"Position after h2h4:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        Console.WriteLine($"En passant square: {position.EnPassantSquare}");
        
        // Now play some black moves and check deeper
        var testMoves = new[] { "g8f6", "e7e5", "e7e6", "g7g5" };
        
        Span<Move> blackMoves = stackalloc Move[218];
        Span<Move> whiteMoves = stackalloc Move[218];
        
        foreach (var moveStr in testMoves)
        {
            Console.WriteLine($"\nChecking after h2h4 {moveStr}:");
            
            // Find the move
            var blackList = new MoveList(blackMoves);
            _moveGenerator.GenerateMoves(position, ref blackList);
            
            Move? targetMove = null;
            for (int i = 0; i < blackList.Count; i++)
            {
                if (blackList[i].ToUci() == moveStr)
                {
                    targetMove = blackList[i];
                    break;
                }
            }
            
            if (!targetMove.HasValue)
            {
                Console.WriteLine($"Move {moveStr} not found!");
                continue;
            }
            
            var blackUndo = position.MakeMove(targetMove.Value);
            
            // Count white's responses
            var whiteList = new MoveList(whiteMoves);
            _moveGenerator.GenerateMoves(position, ref whiteList);
            
            Console.WriteLine($"White has {whiteList.Count} moves");
            
            // Look for en passant captures if applicable
            if (moveStr == "g7g5")
            {
                Console.WriteLine($"En passant square after g7g5: {position.EnPassantSquare}");
                var epCaptures = 0;
                for (int i = 0; i < whiteList.Count; i++)
                {
                    if ((whiteList[i].Flags & MoveType.EnPassant) != 0)
                    {
                        epCaptures++;
                        Console.WriteLine($"En passant capture: {whiteList[i].ToUci()}");
                    }
                }
                Console.WriteLine($"Total en passant captures: {epCaptures}");
            }
            
            position.UnmakeMove(targetMove.Value, blackUndo);
        }
        
        position.UnmakeMove(h2h4, undoInfo);
    }
}