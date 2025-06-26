#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class CheckF1B5Move
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void CheckF1B5InEnPassantPosition()
    {
        // En passant position
        var fenResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        
        // Find and make the f1b5 move
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Move? f1b5Move = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == "f1b5")
            {
                f1b5Move = moves[i];
                break;
            }
        }
        
        Assert.IsTrue(f1b5Move.HasValue, "f1b5 move not found");
        
        // Make the move
        var undoInfo = position.MakeMove(f1b5Move.Value);
        Console.WriteLine($"Position after f1b5:");
        Console.WriteLine($"FEN: {position.ToFen()}");
        
        // Generate moves from this position
        moves.Clear();
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nMoves after f1b5: {moves.Count}");
        
        // Check perft at various depths
        var perft1 = Perft(position, 1);
        var perft2 = Perft(position, 2);
        var perft3 = Perft(position, 3);
        
        Console.WriteLine($"\nPerft from position after f1b5:");
        Console.WriteLine($"Perft(1): {perft1}");
        Console.WriteLine($"Perft(2): {perft2}");
        Console.WriteLine($"Perft(3): {perft3}");
        
        // The user mentioned f1b5 shows 1 node vs Stockfish's 6
        // This might be at depth 3 from the original position
        Console.WriteLine($"\nNote: User reported f1b5 shows 1 node vs Stockfish's 6");
        
        // Let's also check what happens if Black plays b5c6 check
        Move? b5c6Move = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].ToUci() == "b8c6")
            {
                b5c6Move = moves[i];
                break;
            }
        }
        
        if (b5c6Move.HasValue)
        {
            var undoInfo2 = position.MakeMove(b5c6Move.Value);
            Console.WriteLine($"\nAfter b8c6:");
            Console.WriteLine($"FEN: {position.ToFen()}");
            
            moves.Clear();
            _moveGenerator.GenerateMoves(position, ref moves);
            Console.WriteLine($"Legal moves for White: {moves.Count}");
            
            position.UnmakeMove(b5c6Move.Value, undoInfo2);
        }
        
        position.UnmakeMove(f1b5Move.Value, undoInfo);
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