#nullable enable

using Xunit;
using Xunit.Abstractions;
using Meridian.Core.Board;
using Meridian.Core.Search;
using System;

namespace Meridian.Tests.Search;

public class SimulateThreadChangeScenario
{
    private readonly ITestOutputHelper _output;
    
    public SimulateThreadChangeScenario(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void TestExactScenarioFromIssue()
    {
        // This test simulates the exact scenario from the issue:
        // 1. Search from starting position with depth 20
        // 2. Simulate thread count change (which doesn't clear TT)
        // 3. Search again from same position
        
        var engine = new SearchEngine(128);
        var position = Position.StartingPosition();
        
        // First search - depth 20
        _output.WriteLine("=== First Search ===");
        var limits1 = new SearchLimits
        {
            Depth = 20,
            Infinite = false
        };
        
        var searchInfo1 = engine.SearchInfo;
        var move1 = engine.StartSearch(position, limits1);
        
        _output.WriteLine($"Depth reached: {searchInfo1.Depth}");
        _output.WriteLine($"Nodes searched: {searchInfo1.Nodes:N0}");
        _output.WriteLine($"Best move: {move1.ToUci()}");
        _output.WriteLine($"Evaluation: {searchInfo1.Score}");
        
        Assert.NotEqual(Move.None, move1);
        Assert.True(searchInfo1.Nodes > 1000000, $"Expected > 1M nodes, got {searchInfo1.Nodes:N0}");
        
        // In the real scenario, "setoption name Threads value 20" is called here
        // This doesn't clear the TT, so entries from the first search remain
        
        // Second search - same position, same depth
        _output.WriteLine("\n=== Second Search (after simulated thread change) ===");
        var limits2 = new SearchLimits
        {
            Depth = 20,
            Infinite = false
        };
        
        var move2 = engine.StartSearch(position, limits2);
        var searchInfo2 = engine.SearchInfo;
        
        _output.WriteLine($"Depth reached: {searchInfo2.Depth}");
        _output.WriteLine($"Nodes searched: {searchInfo2.Nodes:N0}");
        _output.WriteLine($"Best move: {move2.ToUci()}");
        _output.WriteLine($"Evaluation: {searchInfo2.Score}");
        
        // The bug was: move2 would be Move.None (0000) and only 20 nodes searched
        Assert.NotEqual(Move.None, move2);
        Assert.True(move2.From != Square.None);
        Assert.True(move2.To != Square.None);
        
        // The second search might use fewer nodes due to TT hits, but should still find a move
        _output.WriteLine($"\nNode reduction due to TT: {(1.0 - (double)searchInfo2.Nodes / searchInfo1.Nodes):P1}");
    }
}