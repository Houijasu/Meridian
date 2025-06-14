using Meridian.Core;

namespace Meridian.Debug;

public static class DebugPawnCaptures
{
    public static void Analyze()
    {
        // Position with white pawn on a7
        var board = FenParser.Parse("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1");
        
        Console.WriteLine("Debugging pawn captures from a7:");
        
        // Manual calculation
        ulong whitePawns = board.WhitePawns;
        ulong a7Pawn = whitePawns & Square.A7.ToBitboard();
        
        Console.WriteLine($"White pawns: 0x{whitePawns:X16}");
        Console.WriteLine($"A7 pawn: 0x{a7Pawn:X16}");
        Console.WriteLine($"A7 bit position: {(int)Square.A7}");
        
        // Check shifts
        ulong shiftedNE = Bitboard.ShiftNorthEast(a7Pawn);
        ulong shiftedNW = Bitboard.ShiftNorthWest(a7Pawn);
        
        Console.WriteLine($"\nA7 pawn shifted NE: 0x{shiftedNE:X16}");
        Console.WriteLine($"A7 pawn shifted NW: 0x{shiftedNW:X16}");
        
        // Check if b8 is in the shifted positions
        ulong b8Bit = Square.B8.ToBitboard();
        Console.WriteLine($"\nB8 bit: 0x{b8Bit:X16}");
        Console.WriteLine($"B8 bit position: {(int)Square.B8}");
        
        Console.WriteLine($"Is B8 in NE shift: {(shiftedNE & b8Bit) != 0}");
        Console.WriteLine($"Is B8 in NW shift: {(shiftedNW & b8Bit) != 0}");
        
        // The issue might be the file masks
        Console.WriteLine($"\nFile masks:");
        Console.WriteLine($"NotFileA: 0x{Bitboard.NotFileA:X16}");
        Console.WriteLine($"NotFileH: 0x{Bitboard.NotFileH:X16}");
        
        // A7 is on file A, so ShiftNorthWest won't work (it masks out file A)
        // But ShiftNorthEast should work
        
        // Let's check the manual shift
        ulong manualNE = a7Pawn << 9;  // Northeast is +9
        Console.WriteLine($"\nManual NE shift (a7 << 9): 0x{manualNE:X16}");
        Console.WriteLine($"Is this B8? {manualNE == b8Bit}");
        
        // Now check against black pieces
        Console.WriteLine($"\nBlack pieces: 0x{board.BlackPieces:X16}");
        Console.WriteLine($"B8 occupied by black? {(board.BlackPieces & b8Bit) != 0}");
        
        // Check what's actually on b8
        var (piece, color) = board.GetPieceAt(Square.B8);
        Console.WriteLine($"Piece on B8: {piece} ({color})");
        
        // The problem might be that b8 is empty!
        Console.WriteLine($"\nEmpty squares: 0x{board.EmptySquares:X16}");
        Console.WriteLine($"Is B8 empty? {(board.EmptySquares & b8Bit) != 0}");
    }
}