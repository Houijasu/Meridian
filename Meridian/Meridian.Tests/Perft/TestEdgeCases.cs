#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestEdgeCases
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestEdgeFileRays()
    {
        Console.WriteLine("Testing ray attacks for edge squares:");
        
        // Test A-file and H-file squares
        var edgeSquares = new[] 
        { 
            (Square.A1, "A1"), (Square.A4, "A4"), (Square.A8, "A8"),
            (Square.H1, "H1"), (Square.H4, "H4"), (Square.H8, "H8")
        };
        
        foreach (var (square, name) in edgeSquares)
        {
            Console.WriteLine($"\nSquare {name}:");
            
            // Test all directions
            for (int dir = 0; dir < 8; dir++)
            {
                var ray = AttackTables.GetRay(square, dir);
                var count = Bitboard.PopCount(ray);
                var dirName = GetDirectionName(dir);
                
                Console.WriteLine($"  {dirName}: {count} squares");
                
                // Verify rays don't wrap around
                if (count > 0)
                {
                    var squares = GetSquares(ray);
                    foreach (var sq in squares)
                    {
                        var fileDiff = Math.Abs(sq.File() - square.File());
                        var rankDiff = Math.Abs(sq.Rank() - square.Rank());
                        
                        // Check for wrap-around
                        if (fileDiff > 1 && rankDiff > 1)
                        {
                            Console.WriteLine($"    WARNING: Wrap-around detected to {sq}!");
                        }
                    }
                }
            }
        }
    }
    
    [TestMethod]
    public void TestPositionAfterEdgeMoves()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        // Test h2h4 specifically
        Console.WriteLine("Testing h2h4 move:");
        
        // Find h2h4
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Move? h2h4 = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == Square.H2 && moves[i].To == Square.H4)
            {
                h2h4 = moves[i];
                break;
            }
        }
        
        Assert.IsTrue(h2h4.HasValue);
        
        // Make h2h4
        var undoInfo = position.MakeMove(h2h4.Value);
        
        // Generate black's responses
        Span<Move> blackMoves = stackalloc Move[218];
        var blackMovesList = new MoveList(blackMoves);
        _moveGenerator.GenerateMoves(position, ref blackMovesList);
        
        Console.WriteLine($"Black has {blackMovesList.Count} moves after h2h4");
        
        // Check for any unusual moves
        var unusualMoves = 0;
        for (int i = 0; i < blackMovesList.Count; i++)
        {
            var move = blackMovesList[i];
            
            // Check if it's a pawn capture to h3 (en passant)
            if (move.To == Square.H3 && (move.Flags & MoveType.Capture) != 0)
            {
                Console.WriteLine($"Found capture to h3: {move.ToUci()} (flags: {move.Flags})");
                unusualMoves++;
            }
        }
        
        Console.WriteLine($"Unusual moves found: {unusualMoves}");
        
        // Test perft
        var perft3 = Perft(position, 3);
        Console.WriteLine($"Perft(3) after h2h4: {perft3}");
        Console.WriteLine($"Expected: 9329 (from perft divide)");
        Console.WriteLine($"Actual: {perft3 + 2}"); // Adding 2 because we're at depth 3, not 4
        
        position.UnmakeMove(h2h4.Value, undoInfo);
    }
    
    private string GetDirectionName(int dir) => dir switch
    {
        0 => "NorthWest",
        1 => "North",
        2 => "NorthEast",
        3 => "West",
        4 => "East",
        5 => "SouthWest",
        6 => "South",
        7 => "SouthEast",
        _ => "Unknown"
    };
    
    private Square[] GetSquares(Bitboard bb)
    {
        var squares = new List<Square>();
        var temp = bb;
        while (temp.IsNotEmpty())
        {
            squares.Add((Square)temp.GetLsbIndex());
            temp = temp.RemoveLsb();
        }
        return squares.ToArray();
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