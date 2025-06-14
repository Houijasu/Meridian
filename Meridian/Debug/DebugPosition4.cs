using Meridian.Core;

namespace Meridian.Debug;

public static class DebugPosition4
{
    public static void Analyze()
    {
        // Position 4: r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1
        var board = FenParser.Parse("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1");
        
        Console.WriteLine("Debugging Position 4 (expected 422333, got 422282 - missing 51 nodes at depth 4):");
        Console.WriteLine($"FEN: {FenParser.ToFen(ref board)}");
        Console.WriteLine($"Castling rights: {board.CastlingRights}");
        Console.WriteLine($"En passant: {board.EnPassantSquare}");
        Console.WriteLine();
        
        // The position has a pawn on a7 that can promote
        // Check if promotion is being handled correctly
        Console.WriteLine("Checking promotion squares:");
        Console.WriteLine($"White pawn on a7: {(board.WhitePawns & Square.A7.ToBitboard()) != 0}");
        Console.WriteLine($"Black pawn on a2: {(board.BlackPawns & Square.A2.ToBitboard()) != 0}");
        
        // Generate moves at depth 1
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Console.WriteLine($"\nTotal moves at depth 1: {moves.Count}");
        
        // Look for promotion moves
        int promotions = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].IsPromotion())
            {
                promotions++;
                Console.WriteLine($"Promotion: {moves[i]} -> {moves[i].PromotionPiece}");
            }
        }
        Console.WriteLine($"Total promotions: {promotions}");
        
        // Compare with known perft divide values
        var knownDepth1 = new Dictionary<string, int>
        {
            { "c4c5", 1 },
            { "d2d4", 1 },
            { "f3d4", 1 },
            { "b4c5", 1 },
            { "f1f2", 1 },
            { "g1h1", 1 }
        };
        
        // Count legal moves
        Console.WriteLine("\nLegal moves at depth 1:");
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                string moveStr = moves[i].ToString();
                Console.WriteLine($"  {moveStr}");
                
                // Special attention to a7 pawn moves
                if (moves[i].From == Square.A7)
                {
                    Console.WriteLine($"    -> Promotion move from a7!");
                }
            }
            
            board = copy;
        }
        
        // Deeper analysis - check problematic moves
        Console.WriteLine("\nAnalyzing specific moves at depth 3:");
        string[] problematicMoves = { "c4c5", "g1h1" };
        
        foreach (var moveStr in problematicMoves)
        {
            var from = ParseSquare(moveStr.Substring(0, 2));
            var to = ParseSquare(moveStr.Substring(2, 2));
            
            // Find and make the move
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].From == from && moves[i].To == to)
                {
                    BoardState copy = board;
                    board.MakeMove(moves[i]);
                    
                    if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
                    {
                        ulong nodes = CountNodesAtDepth(ref board, 3);
                        Console.WriteLine($"{moveStr}: {nodes} nodes at depth 3");
                    }
                    
                    board = copy;
                    break;
                }
            }
        }
    }
    
    private static Square ParseSquare(string sq)
    {
        int file = sq[0] - 'a';
        int rank = sq[1] - '1';
        return (Square)(rank * 8 + file);
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
    
    private static ulong CountNodesAtDepth(ref BoardState board, int depth)
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
                nodes += CountNodesAtDepth(ref board, depth - 1);
            }
            
            board = copy;
        }
        
        return nodes;
    }
}