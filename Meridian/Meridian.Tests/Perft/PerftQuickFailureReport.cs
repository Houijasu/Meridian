#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftQuickFailureReport
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void ReportPerftFailuresUpToDepth4()
    {
        var tests = new[]
        {
            // Starting position
            ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 1, 20UL, "Starting position"),
            ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 2, 400UL, "Starting position"),
            ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 3, 8902UL, "Starting position"),
            ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 4, 197281UL, "Starting position"),
            
            // Kiwipete
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 1, 48UL, "Kiwipete"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 2, 2039UL, "Kiwipete"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 3, 97862UL, "Kiwipete"),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 4, 4085603UL, "Kiwipete"),
            
            // Position 3
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 1, 14UL, "Position 3"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 2, 191UL, "Position 3"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 3, 2812UL, "Position 3"),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 4, 43238UL, "Position 3"),
            
            // Position 4
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 1, 6UL, "Position 4"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 2, 264UL, "Position 4"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 3, 9467UL, "Position 4"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 4, 422333UL, "Position 4"),
            
            // Position 5
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 1, 44UL, "Position 5"),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 2, 1486UL, "Position 5"),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 3, 62379UL, "Position 5"),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 4, 2103487UL, "Position 5"),
            
            // Position 6
            ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 1, 46UL, "Position 6"),
            ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 2, 2079UL, "Position 6"),
            ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 3, 89890UL, "Position 6"),
            ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 4, 3894594UL, "Position 6"),
            
            // En passant
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 1, 31UL, "En passant"),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 2, 707UL, "En passant"),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 3, 27837UL, "En passant"),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 4, 824064UL, "En passant"),
            
            // Castling
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 1, 26UL, "Castling"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 2, 568UL, "Castling"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 3, 13744UL, "Castling"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 4, 314346UL, "Castling"),
            
            // Promotion
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 1, 18UL, "Promotion"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 2, 270UL, "Promotion"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 3, 4699UL, "Promotion"),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 4, 73683UL, "Promotion")
        };
        
        Console.WriteLine("## Failed Perft Tests\n");
        
        var failureCount = 0;
        foreach (var (fen, depth, expected, name) in tests)
        {
            var positionResult = Position.FromFen(fen);
            if (!positionResult.IsSuccess) continue;
            
            var actual = Perft(positionResult.Value, depth);
            if (actual != expected)
            {
                failureCount++;
                Console.WriteLine($"**{name} - Depth {depth}**");
                Console.WriteLine($"FEN: `{fen}`");
                Console.WriteLine($"Expected: {expected:N0}");
                Console.WriteLine($"Actual: {actual:N0}");
                Console.WriteLine($"Difference: {(long)actual - (long)expected:+#;-#;0}\n");
            }
        }
        
        if (failureCount == 0)
        {
            Console.WriteLine("All tests passed!");
        }
        else
        {
            Console.WriteLine($"\nTotal failures: {failureCount}");
            Assert.Fail($"Found {failureCount} perft failures");
        }
    }
    
    private ulong Perft(Position position, int depth)
    {
        if (depth == 0) return 1;

        ulong nodes = 0;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undoInfo = position.MakeMove(move);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(move, undoInfo);
        }

        return nodes;
    }
}