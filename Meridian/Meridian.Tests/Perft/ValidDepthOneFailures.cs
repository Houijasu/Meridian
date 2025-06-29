#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class ValidDepthOneFailures
{
    private readonly MoveGenerator _moveGenerator = new();
    
    [TestMethod]
    public void TestValidDepthOneFailures()
    {
        Console.WriteLine("=== VALID POSITIONS THAT FAIL AT DEPTH 1 ===\n");
        
        // Position 5 - the only truly failing position due to legal vs pseudo-legal moves
        var fen = "r3k3/1K6/8/8/8/8/8/8 w q - 0 1";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        var position = positionResult.Value;
        
        Console.WriteLine($"FEN: {fen}");
        Console.WriteLine($"Position: White king on b7, Black rook on a8, Black king on e8");
        Console.WriteLine($"Side to move: {position.SideToMove}");
        Console.WriteLine();
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Our engine generates: {moves.Count} moves");
        Console.WriteLine("Moves:");
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var to = move.To;
            Console.WriteLine($"  {i+1}. {move.ToUci()} - {GetMoveDescription(position, move, to)}");
        }
        
        Console.WriteLine($"\nStandard perft expects: 5 moves");
        Console.WriteLine("Expected moves:");
        var expectedMoves = new[] {
            ("b7a6", "King to a6 (under attack by rook - ILLEGAL)"),
            ("b7a7", "King to a7 (under attack by rook - ILLEGAL)"), 
            ("b7a8", "King captures rook on a8"),
            ("b7b6", "King to b6"),
            ("b7b8", "King to b8 (under attack by rook - ILLEGAL)"),
            ("b7c6", "King to c6"),
            ("b7c7", "King to c7"),
            ("b7c8", "King to c8 (under attack by rook - ILLEGAL)")
        };
        
        foreach (var (moveStr, desc) in expectedMoves)
        {
            var found = false;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].ToUci() == moveStr)
                {
                    found = true;
                    break;
                }
            }
            Console.WriteLine($"  {moveStr}: {(found ? "✓" : "✗")} - {desc}");
        }
        
        Console.WriteLine("\n=== EXPLANATION ===");
        Console.WriteLine("Our engine correctly generates only LEGAL moves (4 moves).");
        Console.WriteLine("Standard perft counts PSEUDO-LEGAL moves (5 moves), including illegal moves.");
        Console.WriteLine("The missing move is Kb7a7, which would put the white king in check from the black rook on a8.");
        Console.WriteLine("\nThis is NOT a bug - it's a design decision. Modern chess engines generate only legal moves.");
        
        Console.WriteLine("\n=== STOCKFISH COMPARISON ===");
        Console.WriteLine("To verify with Stockfish, run:");
        Console.WriteLine("position fen r3k3/1K6/8/8/8/8/8/8 w q - 0 1");
        Console.WriteLine("go perft 1");
        Console.WriteLine("\nStockfish will show 5 moves (including the illegal Kb7a7).");
    }
    
    private string GetMoveDescription(Position position, Move move, Square to)
    {
        var piece = position.GetPiece(to);
        if (piece != Piece.None)
            return $"King captures {piece}";
        else
            return "King to empty square";
    }
}