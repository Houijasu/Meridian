#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class StockfishCommands
{
    [TestMethod]
    public void GenerateAllStockfishCommands()
    {
        var positions = new[]
        {
            ("Starting position", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"),
            ("Kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"),
            ("Position 3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1"),
            ("Position 4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1"),
            ("Position 5 (CPW)", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8"),
            ("Black kingside castling", "4k2r/8/8/8/8/8/8/4K3 b k - 0 1"),
            ("En passant", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3"),
            ("Promotion", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1"),
            ("Position 6", "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10")
        };
        
        Console.WriteLine("=== STOCKFISH VERIFICATION COMMANDS ===\n");
        Console.WriteLine("Run these commands in Stockfish to verify perft values:\n");
        
        foreach (var (name, fen) in positions)
        {
            Console.WriteLine($"# {name}");
            Console.WriteLine($"position fen {fen}");
            Console.WriteLine("go perft 1");
            Console.WriteLine("go perft 2");
            Console.WriteLine("go perft 3");
            Console.WriteLine("go perft 4");
            Console.WriteLine("go perft 5");
            Console.WriteLine("go perft 6");
            Console.WriteLine();
        }
        
        Console.WriteLine("\n=== COPY ALL COMMANDS ===");
        Console.WriteLine("You can copy and paste all these commands at once:\n");
        
        foreach (var (name, fen) in positions)
        {
            Console.WriteLine($"# {name}");
            Console.WriteLine($"position fen {fen}");
            for (int d = 1; d <= 6; d++)
            {
                Console.WriteLine($"go perft {d}");
            }
        }
    }
}