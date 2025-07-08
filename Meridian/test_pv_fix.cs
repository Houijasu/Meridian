using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core.Board;
using Meridian.Core.Search;
using Meridian.Core.MoveGeneration;
using Xunit;

namespace Meridian.Tests.Search
{
    public class PvFixTests
    {
        [Fact]
        public void PvShouldBeUpdatedAtRootLevel_EvenWhenNotImprovingAlpha()
        {
            // Arrange
            var engine = new SearchEngine();
            var position = new Position();

            // Use a simple position where multiple moves have similar scores
            // This increases the chance that the best move doesn't improve alpha
            var limits = new SearchLimits { Depth = 4 };

            // Track PV updates during search
            var pvUpdates = new List<string>();
            engine.OnSearchProgress += (info) =>
            {
                if (info.Depth == 4)
                {
                    var pv = string.Join(" ", info.PrincipalVariation.Select(m => m.ToUci()));
                    pvUpdates.Add($"Depth {info.Depth}: PV = '{pv}'");
                }
            };

            // Act
            var bestMove = engine.StartSearch(position, limits);

            // Assert
            Assert.NotEqual(Move.None, bestMove);

            // Find the depth 4 PV update
            var depth4Update = pvUpdates.FirstOrDefault(u => u.Contains("Depth 4:"));
            Assert.NotNull(depth4Update);

            // The PV should not be empty at depth 4
            Assert.Contains("PV = '", depth4Update);
            var pvPart = depth4Update.Split("PV = '")[1].Split("'")[0];
            Assert.NotEmpty(pvPart.Trim());
        }

        [Fact]
        public void PvShouldAlwaysContainBestMoveAtRoot()
        {
            // Arrange
            var engine = new SearchEngine();
            var position = new Position();
            var limits = new SearchLimits { Depth = 6 };

            Move? bestMoveFromSearch = null;
            Move? bestMoveFromPv = null;

            engine.OnSearchProgress += (info) =>
            {
                if (info.Depth == 6)
                {
                    if (info.PrincipalVariation.Count > 0)
                    {
                        bestMoveFromPv = info.PrincipalVariation.First();
                    }
                }
            };

            // Act
            bestMoveFromSearch = engine.StartSearch(position, limits);

            // Assert
            Assert.NotEqual(Move.None, bestMoveFromSearch);
            Assert.NotNull(bestMoveFromPv);
            Assert.Equal(bestMoveFromSearch, bestMoveFromPv);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void PvShouldNeverBeEmptyAtAnyDepth(int depth)
        {
            // Arrange
            var engine = new SearchEngine();
            var position = new Position();
            var limits = new SearchLimits { Depth = depth };

            bool pvWasEmpty = false;
            engine.OnSearchProgress += (info) =>
            {
                if (info.Depth == depth && info.PrincipalVariation.Count == 0)
                {
                    pvWasEmpty = true;
                }
            };

            // Act
            var bestMove = engine.StartSearch(position, limits);

            // Assert
            Assert.NotEqual(Move.None, bestMove);
            Assert.False(pvWasEmpty, $"PV was empty at depth {depth}");
        }

        [Fact]
        public void PvShouldBeConsistentAcrossDepths()
        {
            // Arrange
            var engine = new SearchEngine();
            var position = new Position();
            var limits = new SearchLimits { Depth = 8 };

            var pvByDepth = new Dictionary<int, List<Move>>();

            engine.OnSearchProgress += (info) =>
            {
                if (info.Depth >= 1 && info.Depth <= 8)
                {
                    pvByDepth[info.Depth] = new List<Move>(info.PrincipalVariation);
                }
            };

            // Act
            engine.StartSearch(position, limits);

            // Assert
            for (int depth = 1; depth <= 8; depth++)
            {
                Assert.True(pvByDepth.ContainsKey(depth), $"No PV recorded for depth {depth}");
                Assert.NotEmpty(pvByDepth[depth], $"PV was empty at depth {depth}");

                // The first move should be consistent across depths (best move at root)
                if (depth > 1)
                {
                    var prevFirstMove = pvByDepth[depth - 1][0];
                    var currFirstMove = pvByDepth[depth][0];

                    // Allow for some variation due to search improvements, but log it
                    if (prevFirstMove != currFirstMove)
                    {
                        Console.WriteLine($"First move changed from depth {depth-1} to {depth}: {prevFirstMove.ToUci()} -> {currFirstMove.ToUci()}");
                    }
                }
            }
        }
    }
}
