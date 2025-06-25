#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftTests
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 1, 20UL)]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 2, 400UL)]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 3, 8902UL)]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 4, 197281UL)]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 5, 4865609UL)]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 6, 119060324UL)]
    public void Perft_StartingPosition(string fen, int depth, ulong expectedNodes)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var nodes = Perft(positionResult.Value, depth);
        Assert.AreEqual(expectedNodes, nodes, $"Perft({depth}) failed for starting position");
    }

    [TestMethod]
    [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 1, 48UL)]
    [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 2, 2039UL)]
    [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 3, 97862UL)]
    [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 4, 4085603UL)]
    [DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 5, 193690690UL)]
    public void Perft_Kiwipete(string fen, int depth, ulong expectedNodes)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var nodes = Perft(positionResult.Value, depth);
        Assert.AreEqual(expectedNodes, nodes, $"Perft({depth}) failed for Kiwipete position");
    }

    [TestMethod]
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 1, 14UL)]
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 2, 191UL)]
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 3, 2812UL)]
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 4, 43238UL)]
    [DataRow("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 5, 674624UL)]
    public void Perft_Position3(string fen, int depth, ulong expectedNodes)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var nodes = Perft(positionResult.Value, depth);
        Assert.AreEqual(expectedNodes, nodes, $"Perft({depth}) failed for position 3");
    }

    [TestMethod]
    [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 1, 6UL)]
    [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 2, 264UL)]
    [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 3, 9467UL)]
    [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 4, 422333UL)]
    [DataRow("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 5, 15833292UL)]
    public void Perft_Position4(string fen, int depth, ulong expectedNodes)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var nodes = Perft(positionResult.Value, depth);
        Assert.AreEqual(expectedNodes, nodes, $"Perft({depth}) failed for position 4");
    }

    [TestMethod]
    [DataRow("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 1, 44UL)]
    [DataRow("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 2, 1486UL)]
    [DataRow("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 3, 62379UL)]
    [DataRow("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 4, 2103487UL)]
    [DataRow("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 5, 89941194UL)]
    public void Perft_Position5(string fen, int depth, ulong expectedNodes)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var nodes = Perft(positionResult.Value, depth);
        Assert.AreEqual(expectedNodes, nodes, $"Perft({depth}) failed for position 5");
    }

    [TestMethod]
    [DataRow("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 1, 46UL)]
    [DataRow("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 2, 2079UL)]
    [DataRow("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 3, 89890UL)]
    [DataRow("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 4, 3894594UL)]
    public void Perft_Position6(string fen, int depth, ulong expectedNodes)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var nodes = Perft(positionResult.Value, depth);
        Assert.AreEqual(expectedNodes, nodes, $"Perft({depth}) failed for position 6");
    }

    [TestMethod]
    public void Perft_EnPassantPosition()
    {
        var tests = new[]
        {
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 1, 31UL),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 2, 908UL),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 3, 27837UL),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 4, 824064UL)
        };

        foreach (var (fen, depth, expected) in tests)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            
            var nodes = Perft(positionResult.Value, depth);
            Assert.AreEqual(expected, nodes, $"Perft({depth}) failed for en passant position");
        }
    }

    [TestMethod]
    public void Perft_CastlingPositions()
    {
        var tests = new[]
        {
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 1, 26UL),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 2, 568UL),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 3, 13744UL),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 4, 314346UL),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", 5, 7594526UL)
        };

        foreach (var (fen, depth, expected) in tests)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            
            var nodes = Perft(positionResult.Value, depth);
            Assert.AreEqual(expected, nodes, $"Perft({depth}) failed for castling position");
        }
    }

    [TestMethod]
    public void Perft_PromotionPositions()
    {
        var tests = new[]
        {
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 1, 18UL),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 2, 270UL),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 3, 4699UL),
            ("8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 4, 73683UL)
        };

        foreach (var (fen, depth, expected) in tests)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            
            var nodes = Perft(positionResult.Value, depth);
            Assert.AreEqual(expected, nodes, $"Perft({depth}) failed for promotion position");
        }
    }

    [TestMethod] 
    public void PerftDivide_StartingPosition()
    {
        var fenResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        var expectedMoves = new Dictionary<string, ulong>
        {
            ["a2a3"] = 380UL,
            ["a2a4"] = 420UL,
            ["b2b3"] = 420UL,
            ["b2b4"] = 421UL,
            ["c2c3"] = 420UL,
            ["c2c4"] = 441UL,
            ["d2d3"] = 539UL,
            ["d2d4"] = 560UL,
            ["e2e3"] = 599UL,
            ["e2e4"] = 600UL,
            ["f2f3"] = 380UL,
            ["f2f4"] = 401UL,
            ["g2g3"] = 420UL,
            ["g2g4"] = 421UL,
            ["h2h3"] = 380UL,
            ["h2h4"] = 420UL,
            ["b1a3"] = 400UL,
            ["b1c3"] = 440UL,
            ["g1f3"] = 440UL,
            ["g1h3"] = 400UL
        };

        var results = PerftDivide(position, 3);
        
        Assert.AreEqual(expectedMoves.Count, results.Count, "Move count mismatch");
        
        foreach (var (moveStr, expectedNodes) in expectedMoves)
        {
            Assert.IsTrue(results.ContainsKey(moveStr), $"Missing move: {moveStr}");
            Assert.AreEqual(expectedNodes, results[moveStr], $"Node count mismatch for move {moveStr}");
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
            var newPosition = ClonePosition(position);
            newPosition.MakeMove(move);
            nodes += Perft(newPosition, depth - 1);
        }

        return nodes;
    }

    private static Position ClonePosition(Position position)
    {
        return new Position(position);
    }

    private Dictionary<string, ulong> PerftDivide(Position position, int depth)
    {
        var results = new Dictionary<string, ulong>();
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var newPosition = ClonePosition(position);
            newPosition.MakeMove(move);
            var nodes = depth > 1 ? Perft(newPosition, depth - 1) : 1;
            results[move.ToUci()] = nodes;
        }

        return results;
    }
}