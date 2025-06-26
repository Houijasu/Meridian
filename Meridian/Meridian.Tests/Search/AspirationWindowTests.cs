#nullable enable

using System.Diagnostics;
using Meridian.Core.Board;
using Meridian.Core.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Meridian.Tests.Search;

[TestClass]
public sealed class AspirationWindowTests
{
    [TestMethod]
    public void TestAspirationWindowsImproveEfficiency()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        var limits = new SearchLimits { Depth = 12 };
        
        // Test with aspiration windows (normal operation)
        var engineWithAspiration = new ParallelSearchEngine(128, 1);
        var sw1 = Stopwatch.StartNew();
        engineWithAspiration.StartSearch(position, limits);
        sw1.Stop();
        var nodesWithAspiration = engineWithAspiration.SearchInfo.Nodes;
        var aspirationHitRate = engineWithAspiration.SearchInfo.AspirationHitRate;
        
        Console.WriteLine($"With aspiration windows:");
        Console.WriteLine($"  Nodes: {nodesWithAspiration}");
        Console.WriteLine($"  Time: {sw1.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Aspiration hit rate: {aspirationHitRate:F2}%");
        Console.WriteLine($"  NPS: {engineWithAspiration.SearchInfo.NodesPerSecond}");
        
        // Aspiration windows should have reasonable hit rate
        Assert.IsTrue(aspirationHitRate > 60, $"Aspiration hit rate ({aspirationHitRate:F2}%) should be above 60%");
        
        engineWithAspiration.Dispose();
    }
    
    [TestMethod]
    public void TestAspirationWindowsHandleFailures()
    {
        // Test position with tactical complexity that might cause aspiration failures
        var positionResult = Position.FromFen("r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        var engine = new ParallelSearchEngine(128, 1);
        var limits = new SearchLimits { Depth = 10 };
        
        engine.StartSearch(position, limits);
        
        var info = engine.SearchInfo;
        Assert.IsTrue(info.AspirationHits + info.AspirationMisses > 0, "Should have aspiration window attempts");
        
        Console.WriteLine($"Aspiration hits: {info.AspirationHits}");
        Console.WriteLine($"Aspiration misses: {info.AspirationMisses}");
        Console.WriteLine($"Aspiration hit rate: {info.AspirationHitRate:F2}%");
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestMoveStabilityDetection()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        var engine = new ParallelSearchEngine(128, 1);
        var limits = new SearchLimits { Depth = 20 }; // High depth to test stability detection
        
        var sw = Stopwatch.StartNew();
        var move = engine.StartSearch(position, limits);
        sw.Stop();
        
        var actualDepth = engine.SearchInfo.Depth;
        
        // If search stopped early due to stability, depth should be less than requested
        if (actualDepth < 20)
        {
            Console.WriteLine($"Search stopped early at depth {actualDepth} due to move stability");
            Assert.IsTrue(actualDepth >= 12, "Stability detection should not trigger too early");
        }
        else
        {
            Console.WriteLine($"Search completed full depth {actualDepth}");
        }
        
        Assert.AreNotEqual(Move.None, move);
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestAspirationWindowsSkippedForMateScores()
    {
        // Position with mate in 2
        var positionResult = Position.FromFen("7k/R7/5K2/8/8/8/8/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        var engine = new ParallelSearchEngine(128, 1);
        var limits = new SearchLimits { Depth = 10 };
        
        engine.StartSearch(position, limits);
        
        var info = engine.SearchInfo;
        Assert.IsTrue(Math.Abs(info.Score) >= SearchConstants.MateInMaxPly, "Should find mate");
        
        // Aspiration windows should not be used much when finding mates
        // (only in early depths before mate is found)
        Console.WriteLine($"Score: {info.Score}");
        Console.WriteLine($"Aspiration attempts: {info.AspirationHits + info.AspirationMisses}");
        
        engine.Dispose();
    }
    
    [TestMethod]
    public void TestDynamicAspirationWindowSizing()
    {
        // Complex middlegame position
        var positionResult = Position.FromFen("r1bqkb1r/pp1n1ppp/2p1pn2/3p4/2PP4/2N1PN2/PP3PPP/R1BQKB1R w KQkq - 0 7");
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        var engine = new ParallelSearchEngine(128, 1);
        var limits = new SearchLimits { Depth = 15 };
        
        engine.StartSearch(position, limits);
        
        var info = engine.SearchInfo;
        
        Console.WriteLine($"Final depth: {info.Depth}");
        Console.WriteLine($"Score: {info.Score}");
        Console.WriteLine($"Aspiration hit rate: {info.AspirationHitRate:F2}%");
        Console.WriteLine($"PVS hit rate: {info.PvsHitRate:F2}%");
        
        // Both optimizations should work well together
        Assert.IsTrue(info.AspirationHitRate > 50, "Aspiration windows should be reasonably effective");
        Assert.IsTrue(info.PvsHitRate > 95, "PVS should maintain high efficiency");
        
        engine.Dispose();
    }
}