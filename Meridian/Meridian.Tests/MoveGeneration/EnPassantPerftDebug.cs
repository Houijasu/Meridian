#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class EnPassantPerftDebug
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void Debug_EnPassant_Perft2()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // First, let's see all moves at depth 1
        Console.WriteLine("=== DEPTH 1 MOVES ===");
        var depth1Results = PerftDivide(position, 1);
        ulong totalDepth1 = 0;
        foreach (var (move, count) in depth1Results.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{move}: {count}");
            totalDepth1 += count;
        }
        Console.WriteLine($"Total at depth 1: {totalDepth1}");
        
        // Now let's specifically check the en passant move
        Console.WriteLine("\n=== EN PASSANT MOVE ANALYSIS ===");
        
        // Find the en passant move
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Move? enPassantMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].IsEnPassant)
            {
                enPassantMove = moves[i];
                break;
            }
        }
        
        if (enPassantMove.HasValue)
        {
            Console.WriteLine($"Found en passant move: {enPassantMove.Value.ToUci()}");
            
            // Make the en passant move and count responses
            var undoInfo = position.MakeMove(enPassantMove.Value);
            
            // Generate all responses
            Span<Move> responseBuffer = stackalloc Move[218];
            var responses = new MoveList(responseBuffer);
            _moveGenerator.GenerateMoves(position, ref responses);
            
            Console.WriteLine($"Black has {responses.Count} responses after exf6");
            
            // Unmake the move
            position.UnmakeMove(enPassantMove.Value, undoInfo);
        }
        else
        {
            Console.WriteLine("No en passant move found!");
        }
        
        // Now let's do full perft(2) and see the difference
        Console.WriteLine("\n=== FULL PERFT(2) ===");
        var perft2 = Perft(position, 2);
        Console.WriteLine($"Perft(2) result: {perft2}");
        Console.WriteLine($"Expected: 908");
        Console.WriteLine($"Difference: {908 - perft2}");
        
        // Let's check each move at depth 2
        Console.WriteLine("\n=== DEPTH 2 BREAKDOWN ===");
        var depth2Results = PerftDivide(position, 2);
        foreach (var (move, count) in depth2Results.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{move}: {count}");
        }
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
            var undoInfo = position.MakeMove(move);
            var nodes = depth > 1 ? Perft(position, depth - 1) : 1;
            position.UnmakeMove(move, undoInfo);
            results[move.ToUci()] = nodes;
        }

        return results;
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