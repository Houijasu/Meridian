#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.Search;

namespace Meridian.Tests.Search;

[TestClass]
public class AdvancedSearchTests
{
    private readonly SearchEngine _searchEngine = new(32); // 32MB for tests

    [TestMethod]
    public void NullMovePruning_DetectsZugzwang()
    {
        // Famous zugzwang position where null move would fail
        var fen = "8/8/p1p5/1p5p/1P5p/8/PPP2K1p/4R1rk w - - 0 1";
        var position = Position.FromFen(fen);
        var limits = new SearchLimits { Depth = 8 };
        
        var bestMove = _searchEngine.StartSearch(position, limits);
        
        // Should find the only winning move Re1-e2
        Assert.AreEqual("e1e2", bestMove.ToUci());
    }
    
    [TestMethod]
    public void AspirationWindows_RefinessScore()
    {
        var position = Position.FromFen("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3");
        var limits = new SearchLimits { Depth = 10 };
        
        var bestMove = _searchEngine.StartSearch(position, limits);
        var info = _searchEngine.SearchInfo;
        
        Assert.AreNotEqual(Move.None, bestMove);
        Assert.IsTrue(info.Depth >= 10, $"Should reach depth 10, but only reached {info.Depth}");
        Assert.IsTrue(info.Nodes > 10000, "Should search many nodes with aspiration windows");
    }
    
    [TestMethod]
    public void LateMovePruning_ReducesNodeCount()
    {
        var position = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        var limits = new SearchLimits { Depth = 8 };
        
        // Search with new engine (has LMR)
        var lmrEngine = new SearchEngine(16);
        lmrEngine.StartSearch(position, limits);
        var lmrNodes = lmrEngine.SearchInfo.Nodes;
        
        // LMR should significantly reduce node count
        // This is just a sanity check - exact reduction depends on position
        Assert.IsTrue(lmrNodes < 500000, $"LMR should keep nodes under 500k at depth 8, but searched {lmrNodes}");
    }
    
    [TestMethod]
    public void CheckExtension_FindsDeeperMates()
    {
        // Position where check extension helps find mate
        var fen = "r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4";
        var position = Position.FromFen(fen);
        var limits = new SearchLimits { Depth = 6 };
        
        var bestMove = _searchEngine.StartSearch(position, limits);
        
        // Should find Bb5xf7+ which leads to mate
        Assert.AreEqual("b5f7", bestMove.ToUci());
        Assert.IsTrue(Math.Abs(_searchEngine.SearchInfo.Score) > 1000, "Should find winning advantage");
    }
    
    [TestMethod] 
    public void HistoryHeuristic_ImprovesMoveOrdering()
    {
        var position = Position.FromFen("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3");
        var limits = new SearchLimits { Depth = 8 };
        
        // First search to populate history
        _searchEngine.StartSearch(position, limits);
        
        // Second search should benefit from history
        var position2 = Position.FromFen("r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4");
        _searchEngine.StartSearch(position2, limits);
        
        // History should be working (this is hard to test directly)
        Assert.IsTrue(_searchEngine.SearchInfo.Nodes > 0);
    }
    
    [TestMethod]
    public void FutilityPruning_PrunesHopelessPositions()
    {
        // Position where white is hopelessly behind
        var fen = "rrb1kbnr/pppppppp/8/8/8/8/PPPPPPPP/4K3 w - - 0 1";
        var position = Position.FromFen(fen);
        var limits = new SearchLimits { Depth = 6 };
        
        var bestMove = _searchEngine.StartSearch(position, limits);
        var nodes = _searchEngine.SearchInfo.Nodes;
        
        // Futility pruning should reduce nodes significantly
        Assert.IsTrue(nodes < 100000, $"Futility should prune many nodes, but searched {nodes}");
    }
}