#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class SimpleFailingPositions
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void ListSimpleFailures()
    {
        Console.WriteLine("=== SIMPLEST FAILING POSITIONS ===\n");
        
        // 1. Three pieces - king moves into check
        Console.WriteLine("1. THREE PIECES (King moves):");
        Console.WriteLine("   FEN: r3k3/1K6/8/8/8/8/8/8 w q - 0 1");
        Console.WriteLine("   Issue: White king on b7 missing moves to a7, a6, b8, c8");
        Console.WriteLine("   These squares are attacked by black rook on a8");
        Console.WriteLine("   Expected: 5 moves, Actual: 4 moves\n");
        
        // 2. Promotion position
        Console.WriteLine("2. PROMOTION (7 pieces):");
        Console.WriteLine("   FEN: 8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1");
        Console.WriteLine("   Issue: Overcount at depth 4 (+5,752 nodes)");
        Console.WriteLine("   Expected: 73,683 nodes, Actual: 79,435 nodes\n");
        
        // 3. En passant position
        Console.WriteLine("3. EN PASSANT (4 pieces):");
        Console.WriteLine("   FEN: 8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1");
        Console.WriteLine("   Expected at depth 1: 15 moves");
        Console.WriteLine("   Expected at depth 4: 13,931 nodes\n");
        
        // Let me check the en passant position
        TestEnPassant4Pieces();
    }
    
    private void TestEnPassant4Pieces()
    {
        var positionResult = Position.FromFen("8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("--- Testing En Passant Position ---");
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine($"En passant square: {position.EnPassantSquare}");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nGenerated {moves.Count} moves:");
        
        var enPassantFound = false;
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            Console.WriteLine($"{i+1}. {move.ToUci()} (flags: {move.Flags})");
            
            if ((move.Flags & MoveType.EnPassant) != 0)
            {
                enPassantFound = true;
                Console.WriteLine("   ^ EN PASSANT!");
            }
        }
        
        Console.WriteLine($"\nEn passant capture c4xd3 found: {enPassantFound}");
        
        // Also test simple promotion
        TestSimplePromotion();
    }
    
    private void TestSimplePromotion()
    {
        // Even simpler promotion position
        var positionResult = Position.FromFen("8/P6k/8/8/8/8/7K/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine("\n--- Testing Simple Promotion ---");
        Console.WriteLine($"Position: {position.ToFen()}");
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nGenerated {moves.Count} moves:");
        
        var promotions = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.From == Square.A7)
            {
                Console.WriteLine($"{move.ToUci()} (flags: {move.Flags})");
                if ((move.Flags & MoveType.Promotion) != 0)
                {
                    promotions++;
                }
            }
        }
        
        Console.WriteLine($"\nPromotion moves found: {promotions} (expected: 4)");
    }
}