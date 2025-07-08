using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core.Board;
using Meridian.Core.Search;

namespace Meridian.Tests
{
    /// <summary>
    /// Simple test to demonstrate and verify the PV (Principal Variation) issue
    /// </summary>
    public class PvTest
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Testing PV output issue...");

            // Create a simple position
            var position = new Position();

            // Create search engine
            var tt = new TranspositionTable(64);
            var searchData = new SearchData();
            var historyScores = new int[2, 64, 64];
            var counterMoves = new Move[64, 64];

            var engine = new SearchEngine(tt, searchData, historyScores, counterMoves);

            // Track PV output for each depth
            var pvResults = new Dictionary<int, string>();

            engine.OnSearchProgress += (info) =>
            {
                var pvMoves = new List<Move>();
                var tempPv = new Queue<Move>(info.PrincipalVariation);
                while (tempPv.Count > 0)
                {
                    pvMoves.Add(tempPv.Dequeue());
                }
                var pvString = string.Join(" ", pvMoves.Select(m => m.ToUci()));

                pvResults[info.Depth] = pvString;

                Console.WriteLine($"Depth {info.Depth}: PV = '{pvString}' (length: {pvMoves.Count})");
            };

            // Run search to depth 8
            var limits = new SearchLimits { Depth = 8 };
            var bestMove = engine.StartSearch(position, limits);

            Console.WriteLine($"\nBest move: {bestMove.ToUci()}");
            Console.WriteLine("\nPV Summary:");

            int missingPvCount = 0;
            for (int depth = 1; depth <= 8; depth++)
            {
                if (pvResults.ContainsKey(depth))
                {
                    var pv = pvResults[depth];
                    if (string.IsNullOrEmpty(pv))
                    {
                        Console.WriteLine($"Depth {depth}: MISSING PV");
                        missingPvCount++;
                    }
                    else
                    {
                        Console.WriteLine($"Depth {depth}: {pv}");
                    }
                }
                else
                {
                    Console.WriteLine($"Depth {depth}: NO INFO REPORTED");
                    missingPvCount++;
                }
            }

            Console.WriteLine($"\nResult: {missingPvCount} depths missing PV out of 8");

            if (missingPvCount > 0)
            {
                Console.WriteLine("ISSUE CONFIRMED: Some depths are missing PV output");
            }
            else
            {
                Console.WriteLine("SUCCESS: All depths have PV output");
            }
        }
    }
}
