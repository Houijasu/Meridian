namespace Meridian.Core.Testing;

using System.Diagnostics;

using MoveGeneration;

/// <summary>
///    Perft (Performance Test) implementation for testing move generation correctness.
/// </summary>
public static class Perft
{
    /// <summary>
    ///    Runs perft test to the specified depth and returns node count.
    /// </summary>
    public static long Run(Position position, int depth)
   {
      if (depth == 0) return 1;

      Span<Move> moveBuffer = stackalloc Move[MoveListFactory.MaxMoves];
      var moveList = new MoveList(moveBuffer);

      MoveGenerator.GenerateMoves(in position, ref moveList);

      long nodes = 0;

      foreach (var move in moveList.Moves)
      {
         var newPosition = position; // Make a copy
         newPosition.MakeMove(move);

         // Skip illegal moves (would leave king in check)
         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;

         nodes += Run(newPosition, depth - 1);
      }

      return nodes;
   }

    /// <summary>
    ///    Runs perft test with detailed move breakdown (divide).
    /// </summary>
    public static void Divide(Position position, int depth)
   {
      Span<Move> moveBuffer = stackalloc Move[MoveListFactory.MaxMoves];
      var moveList = new MoveList(moveBuffer);

      MoveGenerator.GenerateMoves(in position, ref moveList);

      long totalNodes = 0;
      var sw = Stopwatch.StartNew();

      Console.WriteLine($"Perft divide depth {depth}:");
      Console.WriteLine("Move     Nodes");
      Console.WriteLine("--------------");

      foreach (var move in moveList.Moves)
      {
         var newPosition = position;
         newPosition.MakeMove(move);

         // Skip illegal moves
         if (AttackDetection.IsKingInCheck(in newPosition, position.SideToMove))
            continue;

         var nodes = Run(newPosition, depth - 1);
         totalNodes += nodes;

         Console.WriteLine($"{move.ToAlgebraic(),-8} {nodes}");
      }

      sw.Stop();
      var nps = totalNodes * 1000 / Math.Max(1, sw.ElapsedMilliseconds);

      Console.WriteLine("--------------");
      Console.WriteLine($"Total:   {totalNodes}");
      Console.WriteLine($"Time:    {sw.ElapsedMilliseconds}ms");
      Console.WriteLine($"NPS:     {nps:N0}");
   }

    /// <summary>
    ///    Standard perft test positions with expected results.
    /// </summary>
    public static class TestPositions
   {
      public static readonly (string Fen, string Name, long[] Expected)[] Positions = [
         (
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "Starting Position",
            [1, 20, 400, 8902, 197281, 4865609, 119060324]
         ),
         (
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "Kiwipete",
            [1, 48, 2039, 97862, 4085603, 193690690]
         ),
         (
            "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
            "Position 3",
            [1, 14, 191, 2812, 43238, 674624, 11030083]
         ),
         (
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
            "Position 4",
            [1, 6, 264, 9467, 422333, 15833292]
         ),
         (
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
            "Position 5",
            [1, 44, 1486, 62379, 2103487, 89941194]
         ),
         (
            "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
            "Position 6",
            [1, 46, 2079, 89890, 3894594, 164075551]
         )
      ];

      /// <summary>
      ///    Runs all standard perft tests.
      /// </summary>
      public static void RunAllTests(int maxDepth = 4)
      {
         foreach (var (fen, name, expected) in Positions)
         {
            Console.WriteLine($"\nTesting: {name}");
            Console.WriteLine($"FEN: {fen}");

            var position = Fen.Parse(fen);

            for (var depth = 1; depth <= Math.Min(maxDepth, expected.Length - 1); depth++)
            {
               var sw = Stopwatch.StartNew();
               var result = Run(position, depth);
               sw.Stop();

               var passed = result == expected[depth];

               var status = passed
                  ? "PASS"
                  : "FAIL";

               Console.WriteLine($"Depth {depth}: {result,12} (expected {expected[depth],12}) [{status}] {sw.ElapsedMilliseconds}ms");

               if (!passed)
               {
                  Console.WriteLine("Running divide to find error...");
                  Divide(position, depth);
                  break;
               }
            }
         }
      }
   }
}
