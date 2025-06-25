#nullable enable

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using Meridian.Core.Protocol.UCI;

namespace Meridian.Tests.Perft;

[TestClass]
public class PerftDebugger
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    [TestCategory("Debug")]
    public void DebugPosition_FindMissingMoves()
    {
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        var depth = 1;
        var expected = 20UL;
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        var actual = PerftWithDetails(position, depth);
        
        if (actual != expected)
        {
            Console.WriteLine($"Expected: {expected}, Actual: {actual}");
            PrintMoveList(position);
        }
        
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [TestCategory("Debug")]
    public void ComparePerftDivide()
    {
        var fen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        var depth = 2;
        
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        var results = PerftDivideDetailed(position, depth);
        
        Console.WriteLine($"Position: {fen}");
        Console.WriteLine($"Perft({depth}) divide:");
        
        ulong total = 0;
        foreach (var (moveStr, nodes) in results.OrderBy(kvp => kvp.Key))
        {
            Console.WriteLine($"{moveStr}: {nodes}");
            total += nodes;
        }
        
        Console.WriteLine($"Total: {total}");
    }

    [TestMethod]
    [TestCategory("Debug")]
    public void ValidateMoveGeneration_SpecialCases()
    {
        var testCases = new[]
        {
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", "Pinned pieces"),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", "Promotions with check"),
            ("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", "En passant"),
            ("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "Castling"),
            ("8/8/8/8/k7/8/2K5/8 w - - 0 1", "King opposition"),
            ("8/8/8/1k6/3Pp3/8/8/4K3 b - d3 0 1", "En passant discovery check")
        };

        foreach (var (fen, description) in testCases)
        {
            Console.WriteLine($"\nTesting: {description}");
            Console.WriteLine($"FEN: {fen}");
            
            var positionResult = Position.FromFen(fen);
            Assert.IsTrue(positionResult.IsSuccess, $"Failed to parse FEN for {description}");
            
            var position = positionResult.Value;
            PrintMoveList(position);
        }
    }

    [TestMethod]
    [TestCategory("Debug")]
    public void CheckForIllegalMoves()
    {
        var positionResult = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"Checking {moves.Count} moves for legality...");
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var newPosition = ClonePosition(position);
            newPosition.MakeMove(move);
            
            var kingSquare = GetKingSquare(newPosition, position.SideToMove);
            if (kingSquare == Square.None)
            {
                Console.WriteLine($"ILLEGAL: {move.ToUci()} - King captured!");
                Assert.Fail($"Illegal move generated: {move.ToUci()}");
            }
            
            var isInCheck = MoveGenerator.IsSquareAttacked(newPosition, kingSquare, 
                position.SideToMove == Color.White ? Color.Black : Color.White);
            
            if (isInCheck)
            {
                Console.WriteLine($"ILLEGAL: {move.ToUci()} - King left in check!");
                Assert.Fail($"Illegal move generated: {move.ToUci()}");
            }
        }
        
        Console.WriteLine("All moves are legal!");
    }

    private void PrintMoveList(Position position)
    {
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nGenerated {moves.Count} moves:");
        
        var movesByType = new Dictionary<string, List<string>>
        {
            ["Quiet"] = new(),
            ["Capture"] = new(),
            ["DoublePush"] = new(),
            ["EnPassant"] = new(),
            ["Castling"] = new(),
            ["Promotion"] = new()
        };
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var moveStr = move.ToUci();
            
            if ((move.Flags & MoveType.Promotion) != 0)
                movesByType["Promotion"].Add(moveStr);
            else if ((move.Flags & MoveType.Castling) != 0)
                movesByType["Castling"].Add(moveStr);
            else if ((move.Flags & MoveType.EnPassant) != 0)
                movesByType["EnPassant"].Add(moveStr);
            else if ((move.Flags & MoveType.DoublePush) != 0)
                movesByType["DoublePush"].Add(moveStr);
            else if ((move.Flags & MoveType.Capture) != 0)
                movesByType["Capture"].Add(moveStr);
            else
                movesByType["Quiet"].Add(moveStr);
        }
        
        foreach (var (type, movesOfType) in movesByType)
        {
            if (movesOfType.Count > 0)
            {
                Console.WriteLine($"\n{type} moves ({movesOfType.Count}):");
                foreach (var moveStr in movesOfType.OrderBy(m => m))
                {
                    Console.Write($"{moveStr} ");
                }
                Console.WriteLine();
            }
        }
    }

    private ulong PerftWithDetails(Position position, int depth)
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
            var subNodes = Perft(newPosition, depth - 1);
            nodes += subNodes;
            
            if (depth == 1)
            {
                Console.WriteLine($"{move.ToUci()}: {subNodes}");
            }
        }

        return nodes;
    }

    private Dictionary<string, ulong> PerftDivideDetailed(Position position, int depth)
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

    private static Square GetKingSquare(Position position, Color color)
    {
        var king = position.GetBitboard(color, PieceType.King);
        return king.IsEmpty() ? Square.None : (Square)king.GetLsbIndex();
    }

    private static Position ClonePosition(Position position)
    {
        return new Position(position);
    }
}