using Meridian.Core;

namespace Meridian.Debug;

public static class DebugA7Promotion
{
    public static void Analyze()
    {
        // Position with white pawn on a7
        var board = FenParser.Parse("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1");
        
        Console.WriteLine("Debugging a7 pawn promotion:");
        Console.WriteLine($"FEN: {FenParser.ToFen(ref board)}");
        Console.WriteLine();
        
        // Check board state
        Console.WriteLine("Board analysis:");
        Console.WriteLine($"White pawns: 0x{board.WhitePawns:X16}");
        Console.WriteLine($"Pawn on a7: {((board.WhitePawns >> 48) & 1) == 1}");
        Console.WriteLine($"Square a7 index: {(int)Square.A7}");
        Console.WriteLine($"Square a8 index: {(int)Square.A8}");
        Console.WriteLine($"Square b8 index: {(int)Square.B8}");
        Console.WriteLine();
        
        // Check what's on a8 and b8
        var (pieceA8, colorA8) = board.GetPieceAt(Square.A8);
        var (pieceB8, colorB8) = board.GetPieceAt(Square.B8);
        Console.WriteLine($"Piece on a8: {pieceA8} ({colorA8})");
        Console.WriteLine($"Piece on b8: {pieceB8} ({colorB8})");
        Console.WriteLine();
        
        // Manually check promotion moves
        Console.WriteLine("Expected promotion moves from a7:");
        Console.WriteLine("- a7a8 (promotion)");
        Console.WriteLine("- a7b8 (capture promotion)");
        Console.WriteLine();
        
        // Generate all moves
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Console.WriteLine($"Total moves generated: {moves.Count}");
        
        // Look for a7 moves
        Console.WriteLine("\nMoves from a7:");
        int a7Moves = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == Square.A7)
            {
                a7Moves++;
                Console.WriteLine($"  {moves[i]} (Type: {moves[i].Type}, Promo: {moves[i].PromotionPiece})");
            }
        }
        Console.WriteLine($"Total a7 moves: {a7Moves}");
        
        // Let's trace through pawn move generation manually
        Console.WriteLine("\nManual pawn move generation:");
        
        ulong whitePawns = board.WhitePawns;
        ulong enemies = board.BlackPieces;
        ulong empty = board.EmptySquares;
        
        Console.WriteLine($"White pawns bitboard: 0x{whitePawns:X16}");
        Console.WriteLine($"Empty squares: 0x{empty:X16}");
        Console.WriteLine($"Black pieces: 0x{enemies:X16}");
        
        // Single pushes
        ulong singlePushes = Bitboard.ShiftNorth(whitePawns) & empty;
        Console.WriteLine($"\nSingle pushes: 0x{singlePushes:X16}");
        
        // Check if a8 is in single pushes
        bool a8InPushes = (singlePushes & Square.A8.ToBitboard()) != 0;
        Console.WriteLine($"a8 in single pushes: {a8InPushes}");
        
        // Captures
        ulong capturesEast = Bitboard.ShiftNorthEast(whitePawns) & enemies;
        ulong capturesWest = Bitboard.ShiftNorthWest(whitePawns) & enemies;
        
        Console.WriteLine($"\nCaptures east: 0x{capturesEast:X16}");
        Console.WriteLine($"Captures west: 0x{capturesWest:X16}");
        
        bool b8InCaptures = (capturesEast & Square.B8.ToBitboard()) != 0;
        Console.WriteLine($"b8 in captures: {b8InCaptures}");
    }
}