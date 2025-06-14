using Meridian.Core;

namespace Meridian.Debug;

public static class DebugKiwipete
{
    public static void Analyze()
    {
        // Kiwipete position
        var board = FenParser.Parse("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        
        Console.WriteLine("Debugging Kiwipete position (missing 5 nodes at depth 3):");
        Console.WriteLine("FEN: r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Console.WriteLine();
        
        // Known correct perft divide at depth 2 for comparison
        var knownDepth2 = new Dictionary<string, int>
        {
            { "a2a3", 44 }, { "b2b3", 41 }, { "c2c3", 41 }, { "g2g3", 40 }, 
            { "h2g3", 41 }, { "a2a4", 44 }, { "c2c4", 41 }, { "g2g4", 41 },
            { "h2h4", 43 }, { "d5d6", 47 }, { "d5e6", 49 }, { "c3b1", 44 },
            { "c3d1", 42 }, { "c3a4", 45 }, { "c3b5", 45 }, { "e5d3", 40 },
            { "e5c4", 39 }, { "e5g4", 39 }, { "e5c6", 44 }, { "e5g6", 44 },
            { "e5d7", 49 }, { "e5f7", 44 }, { "d2c1", 41 }, { "d2e3", 46 },
            { "d2f4", 41 }, { "d2g5", 44 }, { "d2h6", 41 }, { "e2d1", 42 },
            { "e2f1", 42 }, { "e2d3", 44 }, { "e2c4", 44 }, { "e2b5", 43 },
            { "e2a6", 40 }, { "a1b1", 41 }, { "a1c1", 42 }, { "a1d1", 43 },
            { "h1f1", 41 }, { "h1g1", 42 }, { "f3d3", 41 }, { "f3e3", 47 },
            { "f3g3", 46 }, { "f3h3", 50 }, { "f3f4", 45 }, { "f3g4", 45 },
            { "f3f5", 51 }, { "f3h5", 48 }, { "f3f6", 43 }, { "e1d1", 41 },
            { "e1f1", 42 }, { "e1g1", 43 }, { "e1c1", 43 }
        };
        
        // First let's verify depth 2
        Console.WriteLine("Verifying depth 2 perft divide:");
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        int totalDepth2 = 0;
        var actualDepth2 = new Dictionary<string, int>();
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                int nodes = CountLegalMoves(ref board);
                string moveStr = moves[i].ToString();
                actualDepth2[moveStr] = nodes;
                totalDepth2 += nodes;
                
                if (knownDepth2.TryGetValue(moveStr, out int expected) && nodes != expected)
                {
                    Console.WriteLine($"{moveStr}: {nodes} (expected {expected}) DIFF: {nodes - expected}");
                }
            }
            
            board = copy;
        }
        
        Console.WriteLine($"Total depth 2: {totalDepth2} (expected 2039)");
        Console.WriteLine();
        
        // Now check specific moves that might cause depth 3 issues
        Console.WriteLine("Checking moves with castling rights changes:");
        board = FenParser.Parse("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        
        CheckMove(ref board, "e1g1", "Kingside castle");
        CheckMove(ref board, "e1c1", "Queenside castle");
        CheckMove(ref board, "a1c1", "Rook move (loses Q-side)");
        CheckMove(ref board, "h1g1", "Rook move (loses K-side)");
        CheckMove(ref board, "e1d1", "King move (loses both)");
    }
    
    private static void CheckMove(ref BoardState board, string moveStr, string description)
    {
        Console.WriteLine($"\nChecking {moveStr} ({description}):");
        
        // Parse the move
        var from = ParseSquare(moveStr.Substring(0, 2));
        var to = ParseSquare(moveStr.Substring(2, 2));
        
        // Find the matching move
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Move? targetMove = null;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == from && moves[i].To == to)
            {
                targetMove = moves[i];
                break;
            }
        }
        
        if (targetMove == null)
        {
            Console.WriteLine("  Move not found!");
            return;
        }
        
        BoardState copy = board;
        Console.WriteLine($"  Before: Castling = {board.CastlingRights}");
        board.MakeMove(targetMove.Value);
        Console.WriteLine($"  After:  Castling = {board.CastlingRights}");
        
        // Generate responses and check for castling moves
        MoveList responses = new();
        MoveGenerator.GenerateAllMoves(ref board, ref responses);
        
        int castlingMoves = 0;
        for (int i = 0; i < responses.Count; i++)
        {
            if (responses[i].Type == MoveType.Castle)
            {
                castlingMoves++;
                Console.WriteLine($"  Found castling: {responses[i]}");
            }
        }
        
        Console.WriteLine($"  Total castling moves available: {castlingMoves}");
        
        board = copy;
    }
    
    private static Square ParseSquare(string sq)
    {
        int file = sq[0] - 'a';
        int rank = sq[1] - '1';
        return (Square)(rank * 8 + file);
    }
    
    private static int CountLegalMoves(ref BoardState board)
    {
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        int legal = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                legal++;
            }
            
            board = copy;
        }
        
        return legal;
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}