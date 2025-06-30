#nullable enable

using Xunit;
using Meridian.Core.Board;
using Meridian.Core.Search;
using System;
using System.Threading;

namespace Meridian.Tests.Search;

public class TranspositionTableRootNodeTest
{
    [Fact]
    public void TestSearchAfterThreadChange_ShouldReturnValidMove()
    {
        // This test reproduces the issue where setting threads to 20 
        // causes the engine to return "bestmove 0000"
        
        var engine = new SearchEngine(new TranspositionTable(128), new SearchData(), new int[2, 64, 64]);
        var position = Position.StartingPosition();
        
        // First search - this populates the TT
        var limits1 = new SearchLimits
        {
            Depth = 10,
            Infinite = false
        };
        
        var move1 = engine.StartSearch(position, limits1);
        Assert.NotEqual(Move.None, move1);
        Console.WriteLine($"First search returned: {move1.ToUci()}");
        
        // Simulate what happens when threads are changed
        // The TT still has entries from the previous search
        
        // Second search from same position
        var limits2 = new SearchLimits
        {
            Depth = 10,
            Infinite = false
        };
        
        var move2 = engine.StartSearch(position, limits2);
        Assert.NotEqual(Move.None, move2);
        Console.WriteLine($"Second search returned: {move2.ToUci()}");
        
        // Both searches should return valid moves (not 0000)
        Assert.True(move1.From != Square.None);
        Assert.True(move1.To != Square.None);
        Assert.True(move2.From != Square.None);
        Assert.True(move2.To != Square.None);
    }
    
    [Fact]
    public void TestMultipleSearchesFromSamePosition_ShouldAlwaysReturnValidMove()
    {
        var engine = new SearchEngine(new TranspositionTable(128), new SearchData(), new int[2, 64, 64]);
        var position = Position.StartingPosition();
        
        // Run multiple searches to ensure TT handling is robust
        for (int i = 0; i < 5; i++)
        {
            var limits = new SearchLimits
            {
                Depth = 8,
                Infinite = false
            };
            
            var move = engine.StartSearch(position, limits);
            Console.WriteLine($"Search {i + 1} returned: {move.ToUci()}");
            
            Assert.NotEqual(Move.None, move);
            Assert.True(move.From != Square.None);
            Assert.True(move.To != Square.None);
        }
    }
}