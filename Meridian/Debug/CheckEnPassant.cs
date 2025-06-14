using Meridian.Core;

namespace Meridian.Debug;

public static class CheckEnPassant
{
    public static void VerifyEnPassant()
    {
        Console.WriteLine("Checking en passant legality in Position 3:\n");
        
        // After e2e4
        var board1 = FenParser.Parse("8/2p5/3p4/KP5r/1R2Pp1k/8/6P1/8 b - e3 0 1");
        Console.WriteLine("After e2e4:");
        Console.WriteLine("FEN: 8/2p5/3p4/KP5r/1R2Pp1k/8/6P1/8 b - e3 0 1");
        Console.WriteLine($"En passant square: {board1.EnPassantSquare}");
        
        // Try f4xe3 en passant
        var epMove = new Move(Square.F4, Square.E3, MoveType.EnPassant);
        BoardState copy1 = board1;
        board1.MakeMove(epMove);
        
        bool kingInCheck = IsKingInCheck(ref board1, Color.Black);
        Console.WriteLine($"After f4xe3 (en passant), black king in check: {kingInCheck}");
        Console.WriteLine($"Resulting position: {FenParser.ToFen(ref board1)}");
        board1 = copy1;
        
        Console.WriteLine("\n---\n");
        
        // After g2g4  
        var board2 = FenParser.Parse("8/2p5/3p4/KP5r/1R3pPk/8/4P3/8 b - g3 0 1");
        Console.WriteLine("After g2g4:");
        Console.WriteLine("FEN: 8/2p5/3p4/KP5r/1R3pPk/8/4P3/8 b - g3 0 1");
        Console.WriteLine($"En passant square: {board2.EnPassantSquare}");
        
        // Try f4xg3 en passant
        var epMove2 = new Move(Square.F4, Square.G3, MoveType.EnPassant);
        BoardState copy2 = board2;
        board2.MakeMove(epMove2);
        
        bool kingInCheck2 = IsKingInCheck(ref board2, Color.Black);
        Console.WriteLine($"After f4xg3 (en passant), black king in check: {kingInCheck2}");
        Console.WriteLine($"Resulting position: {FenParser.ToFen(ref board2)}");
        
        // Check if the king would be exposed to the rook
        Console.WriteLine("\nAnalyzing potential discovered check:");
        Console.WriteLine("Black king on h4, White rook on b4");
        Console.WriteLine("If f4 pawn is removed, is there a discovered check along rank 4?");
        
        // Let's check this more carefully
        var testBoard = FenParser.Parse("8/2p5/3p4/KP5r/1R5k/6p1/4P3/8 w - - 0 2");
        Console.WriteLine($"\nTest position (after f4xg3 ep): {FenParser.ToFen(ref testBoard)}");
        bool isCheck = Attacks.IsSquareAttacked(ref testBoard, Square.H4, Color.White);
        Console.WriteLine($"Is h4 attacked by white? {isCheck}");
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}