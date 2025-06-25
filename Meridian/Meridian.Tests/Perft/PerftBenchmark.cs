#nullable enable

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftBenchmark
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    [TestCategory("Benchmark")]
    public void BenchmarkPerft6_StartingPosition()
    {
        var positionResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        var sw = Stopwatch.StartNew();
        
        var nodes = Perft(position, 6);
        
        sw.Stop();
        
        Assert.AreEqual(119060324UL, nodes);
        
        var nps = (ulong)(nodes / sw.Elapsed.TotalSeconds);
        Console.WriteLine($"Perft(6): {nodes} nodes in {sw.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"NPS: {nps:N0}");
        
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 10.0, 
            $"Perft(6) took {sw.Elapsed.TotalSeconds:F2}s, expected < 10s");
    }

    [TestMethod]
    [TestCategory("Benchmark")]
    public void BenchmarkMoveGeneration()
    {
        var positions = new[]
        {
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
            "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10"
        };

        const int iterations = 1_000_000;
        var totalTime = 0.0;
        var totalMoves = 0L;

        foreach (var fen in positions)
        {
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess);
            
            var position = positionResult.Value;
            
            var sw = Stopwatch.StartNew();
            
            for (var i = 0; i < iterations; i++)
            {
                Span<Move> moveBuffer = stackalloc Move[218];
                var moves = new MoveList(moveBuffer);
                _moveGenerator.GenerateMoves(position, ref moves);
                totalMoves += moves.Count;
            }
            
            sw.Stop();
            totalTime += sw.Elapsed.TotalSeconds;
        }

        var avgTimePerPosition = (totalTime / (positions.Length * iterations)) * 1_000_000;
        Console.WriteLine($"Average time per position: {avgTimePerPosition:F2} microseconds");
        Console.WriteLine($"Total moves generated: {totalMoves:N0}");
        
        Assert.IsTrue(avgTimePerPosition < 1.0, 
            $"Move generation took {avgTimePerPosition:F2}μs, expected < 1μs");
    }

    [TestMethod]
    [TestCategory("Benchmark")]
    public void BenchmarkMakeUnmake()
    {
        var positionResult = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        const int iterations = 10_000_000;
        var sw = Stopwatch.StartNew();
        
        for (var i = 0; i < iterations; i++)
        {
            var moveIndex = i % moves.Count;
            var move = moves[moveIndex];
            
            var newPosition = ClonePosition(position);
            newPosition.MakeMove(move);
            _ = newPosition.ZobristKey;
        }
        
        sw.Stop();
        
        var avgTime = (sw.Elapsed.TotalSeconds / iterations) * 1_000_000_000;
        Console.WriteLine($"Average make/unmake time: {avgTime:F2} nanoseconds");
        
        Assert.IsTrue(avgTime < 50.0, 
            $"Make/unmake took {avgTime:F2}ns, expected < 50ns");
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
}