#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class TestPromotionOvercount
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestPromotionPosition()
    {
        // This position is overcounting by 5,752 nodes at depth 4
        var positionResult = Position.FromFen("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"Position: {position.ToFen()}");
        Console.WriteLine("White has 3 pawns on 7th rank ready to promote");
        Console.WriteLine("Black has 3 pawns on 2nd rank ready to promote");
        Console.WriteLine();
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Generated {moves.Count} moves (expected 18):");
        
        var promotions = 0;
        var kingMoves = 0;
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            Console.WriteLine($"{i+1}. {move.ToUci()} (flags: {move.Flags})");
            
            if ((move.Flags & MoveType.Promotion) != 0)
            {
                promotions++;
                Console.WriteLine($"   Promotion to: {move.PromotionType}");
            }
            else if (move.From == Square.E2)
            {
                kingMoves++;
            }
        }
        
        Console.WriteLine($"\nPromotion moves: {promotions} (expected 12 = 3 pawns × 4 pieces)");
        Console.WriteLine($"King moves: {kingMoves} (expected 6)");
        Console.WriteLine($"Total: {moves.Count} (expected 18)");
        
        // Test perft at depth 1 through 4
        Console.WriteLine("\nPerft results:");
        for (int depth = 1; depth <= 4; depth++)
        {
            var nodes = Perft(position, depth);
            var expected = depth switch
            {
                1 => 18UL,
                2 => 270UL,
                3 => 4699UL,
                4 => 73683UL,
                _ => 0UL
            };
            var diff = (long)nodes - (long)expected;
            Console.WriteLine($"Depth {depth}: {nodes:N0} (expected {expected:N0}, diff: {diff:+#;-#;0})");
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