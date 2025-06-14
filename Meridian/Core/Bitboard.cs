namespace Meridian.Core;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

public static class Bitboard
{
    public const ulong FileA = 0x0101010101010101UL;
    public const ulong FileB = 0x0202020202020202UL;
    public const ulong FileC = 0x0404040404040404UL;
    public const ulong FileD = 0x0808080808080808UL;
    public const ulong FileE = 0x1010101010101010UL;
    public const ulong FileF = 0x2020202020202020UL;
    public const ulong FileG = 0x4040404040404040UL;
    public const ulong FileH = 0x8080808080808080UL;

    public const ulong Rank1 = 0x00000000000000FFUL;
    public const ulong Rank2 = 0x000000000000FF00UL;
    public const ulong Rank3 = 0x0000000000FF0000UL;
    public const ulong Rank4 = 0x00000000FF000000UL;
    public const ulong Rank5 = 0x000000FF00000000UL;
    public const ulong Rank6 = 0x0000FF0000000000UL;
    public const ulong Rank7 = 0x00FF000000000000UL;
    public const ulong Rank8 = 0xFF00000000000000UL;

    public const ulong LightSquares = 0x55AA55AA55AA55AAUL;
    public const ulong DarkSquares = 0xAA55AA55AA55AA55UL;

    public const ulong KingSide = FileE | FileF | FileG | FileH;
    public const ulong QueenSide = FileA | FileB | FileC | FileD;
    public const ulong Center = FileD | FileE;
    public const ulong ExtendedCenter = FileC | FileD | FileE | FileF;

    public const ulong NotFileA = ~FileA;
    public const ulong NotFileH = ~FileH;
    public const ulong NotFileAB = ~(FileA | FileB);
    public const ulong NotFileGH = ~(FileG | FileH);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong board)
    {
        return BitOperations.PopCount(board);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitScanForward(ulong board)
    {
        return BitOperations.TrailingZeroCount(board);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitScanReverse(ulong board)
    {
        return 63 - BitOperations.LeadingZeroCount(board);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong SetBit(ulong board, int square)
    {
        return board | (1UL << square);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ClearBit(ulong board, int square)
    {
        return board & ~(1UL << square);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToggleBit(ulong board, int square)
    {
        return board ^ (1UL << square);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetBit(ulong board, int square)
    {
        return (board & (1UL << square)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftNorth(ulong board)
    {
        return board << 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftSouth(ulong board)
    {
        return board >> 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftEast(ulong board)
    {
        return (board & NotFileH) << 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftWest(ulong board)
    {
        return (board & NotFileA) >> 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftNorthEast(ulong board)
    {
        return (board & NotFileH) << 9;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftNorthWest(ulong board)
    {
        return (board & NotFileA) << 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftSouthEast(ulong board)
    {
        return (board & NotFileH) >> 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ShiftSouthWest(ulong board)
    {
        return (board & NotFileA) >> 9;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FillNorth(ulong board)
    {
        board |= board << 8;
        board |= board << 16;
        board |= board << 32;
        return board;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FillSouth(ulong board)
    {
        board |= board >> 8;
        board |= board >> 16;
        board |= board >> 32;
        return board;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopLsb(ref ulong board)
    {
        int square = BitScanForward(board);
        board &= board - 1;
        return square;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pdep(ulong source, ulong mask)
    {
        if (Bmi2.X64.IsSupported)
        {
            return Bmi2.X64.ParallelBitDeposit(source, mask);
        }

        ulong result = 0;
        while (mask != 0)
        {
            int bit = BitScanForward(mask);
            mask &= mask - 1;
            if ((source & 1) != 0)
            {
                result |= 1UL << bit;
            }
            source >>= 1;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pext(ulong source, ulong mask)
    {
        if (Bmi2.X64.IsSupported)
        {
            return Bmi2.X64.ParallelBitExtract(source, mask);
        }

        ulong result = 0;
        int index = 0;
        while (mask != 0)
        {
            int bit = BitScanForward(mask);
            mask &= mask - 1;
            if ((source & (1UL << bit)) != 0)
            {
                result |= 1UL << index;
            }
            index++;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong BishopAttacks(int square, ulong occupied)
    {
        ulong attacks = 0;
        int rank = square / 8;
        int file = square % 8;

        for (int r = rank + 1, f = file + 1; r < 8 && f < 8; r++, f++)
        {
            attacks |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }

        for (int r = rank - 1, f = file + 1; r >= 0 && f < 8; r--, f++)
        {
            attacks |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }

        for (int r = rank + 1, f = file - 1; r < 8 && f >= 0; r++, f--)
        {
            attacks |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }

        for (int r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
        {
            attacks |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }

        return attacks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong RookAttacks(int square, ulong occupied)
    {
        ulong attacks = 0;
        int rank = square / 8;
        int file = square % 8;

        for (int r = rank + 1; r < 8; r++)
        {
            attacks |= 1UL << (r * 8 + file);
            if ((occupied & (1UL << (r * 8 + file))) != 0) break;
        }

        for (int r = rank - 1; r >= 0; r--)
        {
            attacks |= 1UL << (r * 8 + file);
            if ((occupied & (1UL << (r * 8 + file))) != 0) break;
        }

        for (int f = file + 1; f < 8; f++)
        {
            attacks |= 1UL << (rank * 8 + f);
            if ((occupied & (1UL << (rank * 8 + f))) != 0) break;
        }

        for (int f = file - 1; f >= 0; f--)
        {
            attacks |= 1UL << (rank * 8 + f);
            if ((occupied & (1UL << (rank * 8 + f))) != 0) break;
        }

        return attacks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong QueenAttacks(int square, ulong occupied)
    {
        return BishopAttacks(square, occupied) | RookAttacks(square, occupied);
    }
}