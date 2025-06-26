#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestKingCaptures
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestKingCanCaptureAttacker()
    {
        // Position where king on e1 is attacked by queen on d2
        // King should be able to capture the queen
        var positionResult = Position.FromFen("rnb1kbnr/pppppppp/8/8/8/8/PPPqPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine($"White king on: {position.GetBitboard(Color.White, PieceType.King).GetLsbIndex()}");
        Console.WriteLine($"Black queen on d2");
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nTotal moves generated: {moves.Count}");
        
        // Look for king captures
        var kingCaptures = 0;
        var captureD2 = false;
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.From == Square.E1 && (move.Flags & MoveType.Capture) != 0)
            {
                kingCaptures++;
                Console.WriteLine($"King capture found: {move.ToUci()}");
                if (move.To == Square.D2)
                {
                    captureD2 = true;
                }
            }
        }
        
        Console.WriteLine($"\nKing captures found: {kingCaptures}");
        Console.WriteLine($"Can capture queen on d2: {captureD2}");
        
        Assert.IsTrue(captureD2, "King should be able to capture the attacking queen on d2");
    }
    
    [TestMethod]
    public void TestKingMovesUnderAttack()
    {
        // More complex position - Kiwipete at depth 1
        var positionResult = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Kiwipete position - total moves: {moves.Count}");
        Console.WriteLine("Expected: 48");
        
        // Count king moves
        var kingMoves = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == Square.E1)
            {
                kingMoves++;
                Console.WriteLine($"King move: {moves[i].ToUci()}");
            }
        }
        
        Console.WriteLine($"\nTotal king moves: {kingMoves}");
    }
}