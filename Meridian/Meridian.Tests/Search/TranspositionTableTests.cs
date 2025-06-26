#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.Search;

namespace Meridian.Tests.Search;

[TestClass]
public class TranspositionTableTests
{
    [TestMethod]
    public void TranspositionTable_StoreAndProbe()
    {
        var tt = new TranspositionTable(1); // 1MB table
        var key = 0x123456789ABCDEF0UL;
        var score = 150;
        var bestMove = new Move(Square.E2, Square.E4);
        var depth = 10;
        
        // Store entry
        tt.Store(key, score, bestMove, depth, NodeType.Exact, 0);
        
        // Probe should succeed
        var probeResult = tt.Probe(key, depth, -1000, 1000, 0, out var retrievedScore, out var retrievedMove);
        
        Assert.IsTrue(probeResult);
        Assert.AreEqual(score, retrievedScore);
        Assert.AreEqual(bestMove, retrievedMove);
    }
    
    [TestMethod]
    public void TranspositionTable_DepthReplacement()
    {
        var tt = new TranspositionTable(1);
        var key = 0x123456789ABCDEF0UL;
        
        // Store shallow entry
        tt.Store(key, 100, Move.None, 5, NodeType.Exact, 0);
        
        // Store deeper entry with same key
        var deeperMove = new Move(Square.D2, Square.D4);
        tt.Store(key, 200, deeperMove, 10, NodeType.Exact, 0);
        
        // Should retrieve deeper entry
        var probeResult = tt.Probe(key, 10, -1000, 1000, 0, out var score, out var move);
        
        Assert.IsTrue(probeResult);
        Assert.AreEqual(200, score);
        Assert.AreEqual(deeperMove, move);
    }
    
    [TestMethod]
    public void TranspositionTable_BoundTypes()
    {
        var tt = new TranspositionTable(1);
        var key = 0x123456789ABCDEF0UL;
        
        // Test lower bound
        tt.Store(key, 300, Move.None, 8, NodeType.LowerBound, 0);
        
        // Should fail with alpha >= 300
        var probe1 = tt.Probe(key, 8, 400, 500, 0, out _, out _);
        Assert.IsFalse(probe1);
        
        // Should succeed with beta <= 300
        var probe2 = tt.Probe(key, 8, 100, 300, 0, out var score2, out _);
        Assert.IsTrue(probe2);
        Assert.AreEqual(300, score2);
        
        // Test upper bound
        tt.Store(key, -100, Move.None, 8, NodeType.UpperBound, 0);
        
        // Should succeed with alpha >= -100
        var probe3 = tt.Probe(key, 8, -100, 200, 0, out var score3, out _);
        Assert.IsTrue(probe3);
        Assert.AreEqual(-100, score3);
    }
    
    [TestMethod]
    public void TranspositionTable_MateScoreAdjustment()
    {
        var tt = new TranspositionTable(1);
        var key = 0x123456789ABCDEF0UL;
        
        // Test mate score from root (ply 0)
        var mateScore = SearchConstants.MateScore - 5; // Mate in 5 from root
        tt.Store(key, mateScore, Move.None, 10, NodeType.Exact, 0);
        
        // Retrieve from ply 2 (2 moves deeper)
        var probeResult = tt.Probe(key, 10, -32000, 32000, 2, out var retrievedScore, out _);
        
        Assert.IsTrue(probeResult);
        Assert.AreEqual(mateScore - 2, retrievedScore, "Mate score should be adjusted by ply difference");
        
        // Test negative mate score
        var matedScore = -SearchConstants.MateScore + 3; // Mated in 3
        tt.Store(key, matedScore, Move.None, 10, NodeType.Exact, 1);
        
        // Retrieve from ply 3
        probeResult = tt.Probe(key, 10, -32000, 32000, 3, out retrievedScore, out _);
        
        Assert.IsTrue(probeResult);
        Assert.AreEqual(matedScore + 2, retrievedScore, "Negative mate score should be adjusted correctly");
    }
    
    [TestMethod]
    public void TranspositionTable_CollisionHandling()
    {
        var tt = new TranspositionTable(1);
        
        // Find two keys that definitely collide
        var sizeMask = (1 << 16) - 1; // Approximate size for 1MB table
        var key1 = 0x123456789ABCDEF0UL;
        var key2 = key1 ^ (ulong)sizeMask + 1; // Will map to same index
        
        tt.Store(key1, 100, Move.None, 10, NodeType.Exact, 0);
        tt.Store(key2, 200, Move.None, 10, NodeType.Exact, 0);
        
        // Should not retrieve wrong entry due to key validation
        var probe1 = tt.Probe(key1, 10, -1000, 1000, 0, out var score1, out _);
        var probe2 = tt.Probe(key2, 10, -1000, 1000, 0, out var score2, out _);
        
        // Exactly one should succeed (the one stored last)
        Assert.IsTrue(probe1 ^ probe2, "Exactly one probe should succeed after collision");
        
        // Verify no key confusion
        if (probe1)
        {
            Assert.IsFalse(probe2, "If key1 probe succeeds, key2 should fail");
        }
        else
        {
            Assert.IsTrue(probe2, "If key1 probe fails, key2 should succeed");
            Assert.AreEqual(200, score2, "Should retrieve correct score for key2");
        }
    }
    
    [TestMethod]
    public void TranspositionTable_Clear()
    {
        var tt = new TranspositionTable(1);
        var key = 0x123456789ABCDEF0UL;
        
        tt.Store(key, 100, Move.None, 10, NodeType.Exact, 0);
        tt.Clear();
        
        var probeResult = tt.Probe(key, 10, -1000, 1000, 0, out _, out _);
        Assert.IsFalse(probeResult);
    }
    
    [TestMethod]
    public void TranspositionTable_NewSearchAging()
    {
        var tt = new TranspositionTable(1);
        var key = 0x123456789ABCDEF0UL;
        
        // Store in first search
        tt.Store(key, 100, Move.None, 10, NodeType.Exact, 0);
        
        // New search - old entries should be replaceable
        tt.NewSearch();
        
        // Store new entry with lower depth should succeed due to age
        tt.Store(key, 200, Move.None, 5, NodeType.Exact, 0);
        
        var probeResult = tt.Probe(key, 5, -1000, 1000, 0, out var score, out _);
        Assert.IsTrue(probeResult);
        Assert.AreEqual(200, score);
    }
}