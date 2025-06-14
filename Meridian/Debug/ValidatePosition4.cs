using Meridian.Core;

namespace Meridian.Debug;

public static class ValidatePosition4
{
    public static void Analyze()
    {
        // Position 4
        var board = FenParser.Parse("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1");
        
        Console.WriteLine("Position 4 validation:");
        Console.WriteLine($"FEN: {FenParser.ToFen(ref board)}");
        Console.WriteLine();
        
        // The issue: we're missing moves
        // Expected at depth 4: 422,333
        // We get: 422,282
        // Difference: 51 nodes
        
        // Let's think about this position:
        // - White pawn on a7 next to black pawn on b7
        // - Black rook on a8
        // - Empty on b8
        
        // In chess, pawns:
        // 1. Move forward to empty squares
        // 2. Capture diagonally to enemy pieces
        
        // So the a7 pawn:
        // - Cannot move to a8 (occupied by enemy rook)
        // - Cannot capture to a8 (pawns don't capture forward)
        // - Cannot capture to b8 (it's empty)
        
        // Therefore, the a7 pawn has NO legal moves!
        
        Console.WriteLine("A7 pawn analysis:");
        Console.WriteLine("- a8 has black rook (can't move forward to occupied square)");
        Console.WriteLine("- b8 is empty (can't capture to empty square)");
        Console.WriteLine("- Result: No legal moves for a7 pawn");
        Console.WriteLine();
        
        // But wait... let me double-check the FEN
        // "r3k2r/Pppp1ppp/..."
        // Capital P means white pawn
        
        // Actually, I think I need to reread the position...
        // Maybe the error is elsewhere
        
        // Let's generate all moves and see what we get
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Console.WriteLine($"Total moves: {moves.Count}");
        Console.WriteLine("All legal moves:");
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                Console.WriteLine($"  {moves[i]}");
            }
            
            board = copy;
        }
        
        // Actually, let me check if position 4 might have other issues
        // like en passant or special moves
        Console.WriteLine($"\nEn passant square: {board.EnPassantSquare}");
        Console.WriteLine($"Castling rights: {board.CastlingRights}");
        
        // The error might not be with the a7 pawn at all!
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}