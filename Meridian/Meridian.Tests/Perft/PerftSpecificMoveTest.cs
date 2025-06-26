#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftSpecificMoveTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugSpecificMove_h2h4()
    {
        var fenResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        
        // Make h2h4 move
        var h2h4 = new Move(Square.H2, Square.H4, MoveType.DoublePush);
        var undoInfo = position.MakeMove(h2h4);
        
        Console.WriteLine("Position after h2h4:");
        Console.WriteLine(position.ToFen());
        
        // Perft divide at depth 3 from this position
        var results = PerftDivide(position, 3);
        
        // Get expected values from Stockfish for this position
        // We'll compare with actual values
        
        var total = 0UL;
        foreach (var (move, count) in results.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{move}: {count}");
            total += count;
        }
        
        Console.WriteLine($"\nTotal: {total}");
        // Expected total for h2h4 at depth 4 is 9329
        // We're getting 9366, which is 37 more
        
        position.UnmakeMove(h2h4, undoInfo);
    }
    
    [TestMethod]
    public void CompareWithStockfish_h2h4()
    {
        var fenResult = Position.FromFen("rnbqkbnr/pppppppp/8/8/7P/8/PPPPPPP1/RNBQKBNR b KQkq h3 0 1");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        
        // Generate all moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        var output = new System.Text.StringBuilder();
        output.AppendLine($"Black has {moves.Count} moves after h2h4");
        output.AppendLine($"FEN: {position.ToFen()}");
        
        // Look for specific pawn moves
        var pawnMoves = new List<string>();
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var piece = position.GetPiece(move.From);
            if (piece == Piece.BlackPawn)
            {
                pawnMoves.Add(move.ToUci());
            }
        }
        
        output.AppendLine($"\nPawn moves ({pawnMoves.Count}):");
        foreach (var move in pawnMoves.OrderBy(x => x))
        {
            output.AppendLine(move);
        }
        
        System.IO.File.WriteAllText("/tmp/h2h4_moves.txt", output.ToString());
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