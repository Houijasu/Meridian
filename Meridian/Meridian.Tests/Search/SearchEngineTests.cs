#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.Search;

namespace Meridian.Tests.Search;

[TestClass]
public class SearchEngineTests
{
    private readonly SearchEngine _searchEngine = new();

    [TestMethod]
    public void Search_FindsMateInOne()
    {
        var positions = new[]
        {
            ("7k/R7/6K1/8/8/8/8/8 w - - 0 1", "a7a8"),
            ("7k/6K1/5Q2/8/8/8/8/8 w - - 0 1", "f6f8"),
            ("r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4", "b5f7")
        };

        foreach (var (fen, expectedMove) in positions)
        {
            Position position;
            try
            {
                position = Position.FromFen(fen);
            }
            catch (ArgumentException ex)
            {
                Assert.Fail($"Failed to parse FEN: {fen} - {ex.Message}");
                return;
            }
            
            var limits = new SearchLimits { Depth = 3 };
            
            var bestMove = _searchEngine.StartSearch(position, limits);
            
            Assert.AreNotEqual(Move.None, bestMove, $"Failed to find move in position: {fen}");
            Assert.AreEqual(expectedMove, bestMove.ToUci(), $"Wrong move in position: {fen}");
            
            var info = _searchEngine.SearchInfo;
            Assert.IsTrue(Math.Abs(info.Score) >= SearchConstants.MateInMaxPly, 
                $"Should find mate score in position: {fen}, but got score: {info.Score}");
        }
    }

    [TestMethod]
    public void Search_FindsBasicTactics()
    {
        var fen = "r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3";
        var position = Position.FromFen(fen);
        var limits = new SearchLimits { Depth = 5 };
        
        var bestMove = _searchEngine.StartSearch(position, limits);
        
        Assert.AreNotEqual(Move.None, bestMove, "Failed to find any move");
        Assert.IsTrue(_searchEngine.SearchInfo.Score > 0, "Black should have advantage after Nxe4");
    }

    [TestMethod]
    public void Search_RespectsTimeLimit()
    {
        var position = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        var limits = new SearchLimits { MoveTime = 100 };
        
        var startTime = DateTime.UtcNow;
        var bestMove = _searchEngine.StartSearch(position, limits);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        Assert.AreNotEqual(Move.None, bestMove, "Failed to find any move");
        Assert.IsTrue(elapsed < 200, $"Search took too long: {elapsed}ms");
    }

    [TestMethod]
    public void Search_IterativeDeepening()
    {
        var position = Position.FromFen("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3");
        var limits = new SearchLimits { Depth = 6 };
        
        var bestMove = _searchEngine.StartSearch(position, limits);
        
        Assert.AreNotEqual(Move.None, bestMove, "Failed to find any move");
        Assert.AreEqual(6, _searchEngine.SearchInfo.Depth, "Should reach requested depth");
        Assert.IsTrue(_searchEngine.SearchInfo.Nodes > 1000, "Should search many nodes");
    }

    [TestMethod]
    public void Search_DrawDetection()
    {
        var drawPositions = new[]
        {
            "k7/8/K7/8/8/8/8/8 w - - 0 1",
            "k7/8/K7/8/8/8/8/7B w - - 0 1",
            "kb6/8/K7/8/8/8/7B/8 w - - 0 1"
        };

        foreach (var fen in drawPositions)
        {
            var position = Position.FromFen(fen);
            var limits = new SearchLimits { Depth = 5 };
            
            _searchEngine.StartSearch(position, limits);
            
            Assert.AreEqual(0, _searchEngine.SearchInfo.Score, 
                $"Should evaluate as draw: {fen}");
        }
    }
}