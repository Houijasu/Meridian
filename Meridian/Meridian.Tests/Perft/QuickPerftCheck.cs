#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class QuickPerftCheck
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void CheckKeyPositions()
    {
        var tests = new[]
        {
            ("Starting position depth 1", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 1, 20UL),
            ("Starting position depth 2", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 2, 400UL),
            ("Starting position depth 3", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 3, 8902UL),
            ("Starting position depth 4", "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 4, 197281UL),
            ("En passant depth 1", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 1, 31UL),
            ("En passant depth 2", "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", 2, 707UL),
            ("Kiwipete depth 1", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 1, 48UL),
            ("Kiwipete depth 2", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 2, 2039UL),
            ("Promotion depth 1", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 1, 18UL),
            ("Promotion depth 2", "8/PPPk4/8/8/8/8/4Kppp/8 w - - 0 1", 2, 270UL),
        };
        
        foreach (var (name, fen, depth, expected) in tests)
        {
            var positionResult = Position.FromFen(fen);
            if (!positionResult.IsSuccess) continue;
            
            var actual = Perft(positionResult.Value, depth);
            var diff = (long)actual - (long)expected;
            var status = actual == expected ? "PASS" : "FAIL";
            
            Console.WriteLine($"{status} | {name}: expected {expected}, got {actual} (diff: {diff:+#;-#;0})");
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