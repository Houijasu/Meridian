namespace Meridian.Core;

using System.Diagnostics;
using System.Runtime.CompilerServices;

public static class Perft
{
    public static void RunPerftSuite()
    {
        Console.WriteLine("Running Perft Test Suite...\n");
        
        // Standard positions with known perft values
        TestPosition("Starting Position", 
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            new[] { 20UL, 400UL, 8902UL, 197281UL, 4865609UL });
        
        TestPosition("Position 2 (Kiwipete)", 
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            new[] { 48UL, 2039UL, 97862UL, 4085603UL });
        
        TestPosition("Position 3", 
            "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
            new[] { 14UL, 191UL, 2812UL, 43238UL, 674624UL });
        
        TestPosition("Position 4", 
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
            new[] { 6UL, 264UL, 9467UL, 422333UL });
        
        TestPosition("Position 5", 
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
            new[] { 44UL, 1486UL, 62379UL, 2103487UL });
        
        TestPosition("Position 6",
            "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
            new[] { 46UL, 2079UL, 89890UL, 3894594UL });
    }

    private static void TestPosition(string name, string fen, ulong[] expectedNodes)
    {
        Console.WriteLine($"Testing: {name}");
        Console.WriteLine($"FEN: {fen}");
        
        var board = FenParser.Parse(fen);
        var sw = new Stopwatch();
        
        for (int depth = 1; depth <= expectedNodes.Length; depth++)
        {
            sw.Restart();
            ulong nodes = PerftRoot(ref board, depth);
            sw.Stop();
            
            bool passed = nodes == expectedNodes[depth - 1];
            string status = passed ? "PASS" : "FAIL";
            
            Console.WriteLine($"  Depth {depth}: {nodes,12:N0} nodes [{expectedNodes[depth - 1],12:N0} expected] " +
                            $"[{sw.Elapsed.TotalSeconds:F2}s] [{nodes / sw.Elapsed.TotalSeconds:N0} nps] {status}");
            
            if (!passed)
            {
                Console.WriteLine($"  ERROR: Expected {expectedNodes[depth - 1]}, got {nodes}");
                PerftDivide(ref board, depth);
                break;
            }
        }
        Console.WriteLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static ulong PerftRoot(ref BoardState board, int depth)
    {
        if (depth == 0) return 1;
        
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        ulong nodes = 0;
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                nodes += PerftInternal(ref board, depth - 1);
            }
            
            board = copy;
        }
        
        return nodes;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong PerftInternal(ref BoardState board, int depth)
    {
        if (depth == 0) return 1;
        
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        ulong nodes = 0;
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                nodes += PerftInternal(ref board, depth - 1);
            }
            
            board = copy;
        }
        
        return nodes;
    }

    public static void PerftDivide(ref BoardState board, int depth)
    {
        Console.WriteLine("\nPerft Divide:");
        
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        ulong total = 0;
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                ulong nodes = depth > 1 ? PerftInternal(ref board, depth - 1) : 1;
                Console.WriteLine($"  {moves[i]}: {nodes}");
                total += nodes;
            }
            
            board = copy;
        }
        
        Console.WriteLine($"  Total: {total}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false; // No king to be in check
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}