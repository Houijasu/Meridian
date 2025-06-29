#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Collections.Generic;

namespace Meridian.Tests.Perft;

[TestClass]
[TestCategory("Perft")]
public class AnalyzeKiwipeteIllegalMoves
{
    private readonly MoveGenerator _moveGenerator = new();
    private const string KiwipeteFen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

    [TestMethod]
    public void CheckIllegalMoves()
    {
        var positionResult = Position.FromFen(KiwipeteFen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;

        Console.WriteLine($"Analyzing Kiwipete position: {KiwipeteFen}");
        Console.WriteLine();

        var illegalMoves = new List<string>
        {
            "a2a4", "d2c1", "d2e3", "d2f4", "d2g5", "d2h6",
            "e2a6", "e2b5", "f3f5", "g2g3", "g2g4", "g2h3"
        };

        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        var generatedMoves = new HashSet<string>();
        for (var i = 0; i < moves.Count; i++)
        {
            generatedMoves.Add(moves[i].ToUci());
        }

        Console.WriteLine("Checking for illegal moves that were generated:");
        foreach (var illegalMove in illegalMoves)
        {
            if (generatedMoves.Contains(illegalMove))
            {
                Console.WriteLine($"  ILLEGAL MOVE GENERATED: {illegalMove}");
                
                var from = ParseSquare(illegalMove.Substring(0, 2));
                var to = ParseSquare(illegalMove.Substring(2, 2));
                var piece = position.GetPiece(from);
                var targetPiece = position.GetPiece(to);
                
                Console.WriteLine($"    From: {from} ({piece})");
                Console.WriteLine($"    To: {to} ({targetPiece})");
                
                if (illegalMove.StartsWith("d2") || illegalMove.StartsWith("e2"))
                {
                    Console.WriteLine($"    Bishop on {illegalMove.Substring(0, 2)} trying to move illegally");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Expected moves not found:");
        var expectedMoves = new HashSet<string>
        {
            "d2d3", "d2d4", "e2f3", "e2g4", "c2c3", "h2h3", "h2h4", 
            "f3f7", "f3a3", "f3b3", "f3c3", "e1e2"
        };

        foreach (var expectedMove in expectedMoves)
        {
            if (!generatedMoves.Contains(expectedMove))
            {
                Console.WriteLine($"  MISSING EXPECTED MOVE: {expectedMove}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total moves generated: {moves.Count}");
        Console.WriteLine($"Should be: 48");
    }

    private Square ParseSquare(string square)
    {
        var file = square[0] - 'a';
        var rank = square[1] - '1';
        return (Square)(rank * 8 + file);
    }
}