#nullable enable

using System.Diagnostics;
using Meridian.Core.Board;
using Meridian.Core.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Meridian.Tests.Search;

[TestClass]
public sealed class PvsTests
{
    [TestMethod]
    public void TestPvsStatistics()
    {
        var engine = new ParallelSearchEngine(128, 1);
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Depth = 10 };
        
        var move = engine.StartSearch(position, limits);
        var info = engine.SearchInfo;
        
        Assert.AreNotEqual(Move.None, move);
        Assert.IsTrue(info.PvsReSearches > 0, "PVS should trigger re-searches");
        Assert.IsTrue(info.PvsHitRate > 0, "PVS should have some successful re-searches");
        
        Console.WriteLine($"PVS Re-searches: {info.PvsReSearches}");
        Console.WriteLine($"PVS Hits: {info.PvsHits}");
        Console.WriteLine($"PVS Hit Rate: {info.PvsHitRate:F2}%");
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestPvsEfficiency()
    {
        var position = Position.StartingPosition();
        var limits = new SearchLimits { MoveTime = 1000 }; // 1 second search
        
        // Test with single thread to ensure deterministic behavior
        var engine = new ParallelSearchEngine(128, 1);
        engine.StartSearch(position, limits);
        
        var nodes = engine.SearchInfo.Nodes;
        var depth = engine.SearchInfo.Depth;
        var reSearches = engine.SearchInfo.PvsReSearches;
        var hitRate = engine.SearchInfo.PvsHitRate;
        
        engine.Dispose();
        
        // PVS should achieve reasonable efficiency
        Assert.IsTrue(hitRate > 70, $"PVS hit rate ({hitRate:F2}%) should be above 70%");
        Assert.IsTrue(reSearches > 100, "Should have significant number of re-searches in 1 second");
        
        Console.WriteLine($"Depth reached: {depth}");
        Console.WriteLine($"Nodes searched: {nodes}");
        Console.WriteLine($"PVS efficiency: {hitRate:F2}%");
    }
    
    [TestMethod]
    public void TestPvsMoveOrdering()
    {
        // Test that PV moves are properly prioritized
        var positionResult = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        
        var engine = new ParallelSearchEngine(128, 1);
        var limits = new SearchLimits { Depth = 8 };
        
        // First search to establish PV
        var move1 = engine.StartSearch(positionResult.Value, limits);
        var pv1 = engine.SearchInfo.PrincipalVariation.ToList();
        
        // Clear TT to ensure consistent results
        engine.ResizeTranspositionTable(128);
        
        // Second search should follow similar PV
        var move2 = engine.StartSearch(positionResult.Value, limits);
        var pv2 = engine.SearchInfo.PrincipalVariation.ToList();
        
        Assert.AreEqual(move1, move2, "Same position at same depth should produce same best move");
        Assert.IsTrue(pv1.Count > 0 && pv2.Count > 0, "Should have principal variations");
        
        // At least the first few moves should match
        var matchingMoves = 0;
        for (var i = 0; i < Math.Min(3, Math.Min(pv1.Count, pv2.Count)); i++)
        {
            if (pv1[i] == pv2[i])
                matchingMoves++;
        }
        
        Assert.IsTrue(matchingMoves >= 2, "Principal variations should be consistent");
        
        engine.Dispose();
    }
    
    [TestMethod]
    [DataRow("2rr3k/pp3pp1/1nnqbN1p/3pN3/2pP4/2P3Q1/PPB4P/R4RK1 w - - 0 1", 8)] // Tactical position
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 12)] // Endgame
    [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 10)] // Complex middlegame
    public void TestPvsInVariousPositions(string fen, int depth)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var engine = new ParallelSearchEngine(128, 1);
        var limits = new SearchLimits { Depth = depth };
        
        var sw = Stopwatch.StartNew();
        var move = engine.StartSearch(positionResult.Value, limits);
        sw.Stop();
        
        var info = engine.SearchInfo;
        
        Assert.AreNotEqual(Move.None, move);
        Assert.IsTrue(info.Depth >= depth || Math.Abs(info.Score) > SearchConstants.MateInMaxPly, 
            $"Should reach target depth {depth} or find mate, but only reached depth {info.Depth} with score {info.Score}");
        
        Console.WriteLine($"Position: {fen}");
        Console.WriteLine($"Best move: {move.ToUci()}");
        Console.WriteLine($"Score: {info.Score}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Nodes: {info.Nodes}");
        Console.WriteLine($"NPS: {info.NodesPerSecond}");
        Console.WriteLine($"PVS Hit Rate: {info.PvsHitRate:F2}%");
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestPvsWithMultipleThreads()
    {
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Depth = 10 };
        
        // Test with multiple threads
        var engine = new ParallelSearchEngine(128, 4);
        var move = engine.StartSearch(position, limits);
        var info = engine.SearchInfo;
        
        Assert.AreNotEqual(Move.None, move);
        
        // With multiple threads, we should still see PVS working
        // though statistics might vary due to thread racing
        Assert.IsTrue(info.PvsReSearches > 0, "PVS should work with multiple threads");
        
        Console.WriteLine($"Threads: 4");
        Console.WriteLine($"PVS Re-searches: {info.PvsReSearches}");
        Console.WriteLine($"PVS Hit Rate: {info.PvsHitRate:F2}%");
        
        engine.Dispose();
    }
}