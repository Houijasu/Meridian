namespace Meridian.Core;

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
///    Provides high-performance bitboard operations for chess engine.
///    A bitboard is a 64-bit value where each bit represents a square on the chess board.
/// </summary>
public static class Bitboard
{
    /// <summary>
    ///    Gets a bitboard with a single bit set at the specified square.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong SquareBit(int square) => 1UL << square;

    /// <summary>
    ///    Sets a bit at the specified square in the bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong SetBit(ulong bitboard, int square) => bitboard | 1UL << square;

    /// <summary>
    ///    Clears a bit at the specified square in the bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ClearBit(ulong bitboard, int square) => bitboard & ~(1UL << square);

    /// <summary>
    ///    Toggles a bit at the specified square in the bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ToggleBit(ulong bitboard, int square) => bitboard ^ 1UL << square;

    /// <summary>
    ///    Tests if a bit is set at the specified square.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool TestBit(ulong bitboard, int square) => (bitboard & 1UL << square) != 0;

    /// <summary>
    ///    Counts the number of set bits in the bitboard using hardware intrinsics when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static int PopCount(ulong bitboard) => BitOperations.PopCount(bitboard);

    /// <summary>
    ///    Gets the index of the least significant set bit and removes it from the bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static int PopLsb(ref ulong bitboard)
   {
      var lsb = BitOperations.TrailingZeroCount(bitboard);
      bitboard &= bitboard - 1; // Clear the LSB
      return lsb;
   }

    /// <summary>
    ///    Gets the index of the least significant set bit without modifying the bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static int GetLsb(ulong bitboard) => BitOperations.TrailingZeroCount(bitboard);

    /// <summary>
    ///    Gets the index of the most significant set bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static int GetMsb(ulong bitboard) => 63 - BitOperations.LeadingZeroCount(bitboard);

    /// <summary>
    ///    Extracts the least significant set bit as a bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ExtractLsb(ulong bitboard) => bitboard & (ulong)-(long)bitboard;

    /// <summary>
    ///    Shifts bitboard one rank up (towards rank 8).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftNorth(ulong bitboard) => bitboard << 8;

    /// <summary>
    ///    Shifts bitboard one rank down (towards rank 1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftSouth(ulong bitboard) => bitboard >> 8;

    /// <summary>
    ///    Shifts bitboard one file right (towards h-file).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftEast(ulong bitboard) => (bitboard & ~Files.H) << 1;

    /// <summary>
    ///    Shifts bitboard one file left (towards a-file).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftWest(ulong bitboard) => (bitboard & ~Files.A) >> 1;

    /// <summary>
    ///    Shifts bitboard diagonally (north-east).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftNorthEast(ulong bitboard) => (bitboard & ~Files.H) << 9;

    /// <summary>
    ///    Shifts bitboard diagonally (north-west).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftNorthWest(ulong bitboard) => (bitboard & ~Files.A) << 7;

    /// <summary>
    ///    Shifts bitboard diagonally (south-east).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftSouthEast(ulong bitboard) => (bitboard & ~Files.H) >> 7;

    /// <summary>
    ///    Shifts bitboard diagonally (south-west).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong ShiftSouthWest(ulong bitboard) => (bitboard & ~Files.A) >> 9;

    /// <summary>
    ///    Reverses the bitboard (useful for flipping perspectives).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong Reverse(ulong bitboard) => BinaryPrimitives.ReverseEndianness(bitboard);

    /// <summary>
    ///    Prints a bitboard in a human-readable chess board format.
    /// </summary>
    public static void Print(ulong bitboard)
   {
      Console.WriteLine("  a b c d e f g h");
      Console.WriteLine("  ---------------");

      for (var rank = 7; rank >= 0; rank--)
      {
         Console.Write($"{rank + 1}|");

         for (var file = 0; file < 8; file++)
         {
            var square = rank * 8 + file;

            Console.Write(TestBit(bitboard, square)
               ? "X "
               : ". ");
         }

         Console.WriteLine($"|{rank + 1}");
      }

      Console.WriteLine("  ---------------");
      Console.WriteLine("  a b c d e f g h");
      Console.WriteLine($"Bitboard: 0x{bitboard:X16}");
   }
}

/// <summary>
///    Predefined bitboards for files.
/// </summary>
public static class Files
{
   public const ulong A = 0x0101010101010101UL;
   public const ulong B = 0x0202020202020202UL;
   public const ulong C = 0x0404040404040404UL;
   public const ulong D = 0x0808080808080808UL;
   public const ulong E = 0x1010101010101010UL;
   public const ulong F = 0x2020202020202020UL;
   public const ulong G = 0x4040404040404040UL;
   public const ulong H = 0x8080808080808080UL;
}

/// <summary>
///    Predefined bitboards for ranks.
/// </summary>
public static class Ranks
{
   public const ulong Rank1 = 0x00000000000000FFUL;
   public const ulong Rank2 = 0x000000000000FF00UL;
   public const ulong Rank3 = 0x0000000000FF0000UL;
   public const ulong Rank4 = 0x00000000FF000000UL;
   public const ulong Rank5 = 0x000000FF00000000UL;
   public const ulong Rank6 = 0x0000FF0000000000UL;
   public const ulong Rank7 = 0x00FF000000000000UL;
   public const ulong Rank8 = 0xFF00000000000000UL;
}
