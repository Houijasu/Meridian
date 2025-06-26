#nullable enable

using System.Diagnostics;
using Meridian.Core.Board;
using Meridian.Core.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Meridian.Tests.Search;

[TestClass]
public sealed class ParallelSearchTests
{
    [TestMethod]
    public void TestSingleThreadSearch()
    {
        var engine = new ParallelSearchEngine(128, 1);
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Depth = 10 };
        
        var move = engine.StartSearch(position, limits);
        
        Assert.IsNotNull(move);
        Assert.AreNotEqual(Move.None, move);
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestMultiThreadSearch()
    {
        var engine = new ParallelSearchEngine(128, 4);
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Depth = 10 };
        
        var move = engine.StartSearch(position, limits);
        
        Assert.IsNotNull(move);
        Assert.AreNotEqual(Move.None, move);
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestThreadScaling()
    {
        var position = Position.StartingPosition();
        var limits = new SearchLimits { MoveTime = 1000 }; // 1 second search
        
        // Test with 1 thread
        var engine1 = new ParallelSearchEngine(128, 1);
        var sw1 = Stopwatch.StartNew();
        engine1.StartSearch(position, limits);
        sw1.Stop();
        var nodes1 = engine1.SearchInfo.Nodes;
        engine1.Dispose();
        
        // Test with 4 threads
        var engine4 = new ParallelSearchEngine(128, 4);
        var sw4 = Stopwatch.StartNew();
        engine4.StartSearch(position, limits);
        sw4.Stop();
        var nodes4 = engine4.SearchInfo.Nodes;
        engine4.Dispose();
        
        // With 4 threads, we should search significantly more nodes
        Assert.IsTrue(nodes4 > nodes1 * 1.5, 
            $"4 threads ({nodes4} nodes) should search at least 1.5x more nodes than 1 thread ({nodes1} nodes)");
    }
    
    [TestMethod]
    public void TestThreadCountChange()
    {
        var engine = new ParallelSearchEngine(128, 1);
        
        Assert.AreEqual(1, engine.ThreadCount);
        
        engine.SetThreadCount(4);
        Assert.AreEqual(4, engine.ThreadCount);
        
        // Test that search still works after thread count change
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Depth = 8 };
        var move = engine.StartSearch(position, limits);
        
        Assert.AreNotEqual(Move.None, move);
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestDeterministicResults()
    {
        // Test that single-threaded search is deterministic
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Depth = 10 };
        
        var engine1 = new ParallelSearchEngine(128, 1);
        var move1 = engine1.StartSearch(position, limits);
        var score1 = engine1.SearchInfo.Score;
        engine1.Dispose();
        
        var engine2 = new ParallelSearchEngine(128, 1);
        var move2 = engine2.StartSearch(position, limits);
        var score2 = engine2.SearchInfo.Score;
        engine2.Dispose();
        
        Assert.AreEqual(move1, move2, "Single-threaded search should be deterministic");
        Assert.AreEqual(score1, score2, "Scores should be identical for same position and depth");
    }
    
    [TestMethod]
    public void TestStopSearch()
    {
        var engine = new ParallelSearchEngine(128, 4);
        var position = Position.StartingPosition();
        var limits = new SearchLimits { Infinite = true };
        
        // Start search in background
        var searchTask = Task.Run(() => engine.StartSearch(position, limits));
        
        // Let it run for a bit
        Thread.Sleep(100);
        
        // Stop the search
        engine.Stop();
        
        // Wait for search to complete
        var completed = searchTask.Wait(1000);
        Assert.IsTrue(completed, "Search should stop within 1 second");
        
        engine.Dispose();
    }
    
    [TestMethod]
    [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
    [DataRow("rnbqkb1r/pp1ppppp/5n2/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 0 1")]
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]
    public void TestComplexPositions(string fen)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        var limits = new SearchLimits { Depth = 12 };
        
        // Test with multiple threads
        var engine = new ParallelSearchEngine(128, 4);
        var move = engine.StartSearch(position, limits);
        
        Assert.AreNotEqual(Move.None, move, $"Should find a move in position: {fen}");
        Assert.IsTrue(engine.SearchInfo.Nodes > 0, "Should search some nodes");
        
        engine.Dispose();
    }
}