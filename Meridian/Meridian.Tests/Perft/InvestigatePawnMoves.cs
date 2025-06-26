#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class InvestigatePawnMoves
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void CheckEnPassantSquareSetting()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        // Test various double pawn pushes
        var doublePawnPushes = new[]
        {
            ("e2", "e4", Square.E3),
            ("h2", "h4", Square.H3),
            ("b2", "b4", Square.B3),
            ("g2", "g4", Square.G3)
        };
        
        foreach (var (from, to, expectedEp) in doublePawnPushes)
        {
            var fromSquare = SquareExtensions.ParseSquare(from);
            var toSquare = SquareExtensions.ParseSquare(to);
            
            // Find the move
            Span<Move> moveBuffer = stackalloc Move[218];
            var moves = new MoveList(moveBuffer);
            _moveGenerator.GenerateMoves(position, ref moves);
            
            Move? targetMove = null;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].From == fromSquare && moves[i].To == toSquare)
                {
                    targetMove = moves[i];
                    break;
                }
            }
            
            Assert.IsTrue(targetMove.HasValue, $"Move {from}{to} not found");
            
            Console.WriteLine($"\nTesting {from}{to}:");
            Console.WriteLine($"Move flags: {targetMove.Value.Flags}");
            
            // Make the move
            var undoInfo = position.MakeMove(targetMove.Value);
            
            Console.WriteLine($"En passant square after move: {position.EnPassantSquare}");
            Console.WriteLine($"Expected en passant square: {expectedEp}");
            
            // Check if black can capture en passant
            if (position.EnPassantSquare != Square.None)
            {
                // Generate black moves
                Span<Move> blackMoves = stackalloc Move[218];
                var blackMovesList = new MoveList(blackMoves);
                _moveGenerator.GenerateMoves(position, ref blackMovesList);
                
                var epCaptures = 0;
                for (int i = 0; i < blackMovesList.Count; i++)
                {
                    if ((blackMovesList[i].Flags & MoveType.EnPassant) != 0)
                    {
                        epCaptures++;
                        Console.WriteLine($"En passant capture found: {blackMovesList[i].ToUci()}");
                    }
                }
                Console.WriteLine($"Total en passant captures available: {epCaptures}");
            }
            
            // Check perft after the double pawn push
            var perft1 = Perft(position, 1);
            var perft2 = Perft(position, 2);
            Console.WriteLine($"Perft(1) after {from}{to}: {perft1}");
            Console.WriteLine($"Perft(2) after {from}{to}: {perft2}");
            
            position.UnmakeMove(targetMove.Value, undoInfo);
        }
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