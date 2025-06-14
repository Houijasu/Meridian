namespace Meridian.Core;

using System.Runtime.CompilerServices;

public enum Square : byte
{
    A1, B1, C1, D1, E1, F1, G1, H1,
    A2, B2, C2, D2, E2, F2, G2, H2,
    A3, B3, C3, D3, E3, F3, G3, H3,
    A4, B4, C4, D4, E4, F4, G4, H4,
    A5, B5, C5, D5, E5, F5, G5, H5,
    A6, B6, C6, D6, E6, F6, G6, H6,
    A7, B7, C7, D7, E7, F7, G7, H7,
    A8, B8, C8, D8, E8, F8, G8, H8,
    None = 64
}

public enum File : byte
{
    FileA, FileB, FileC, FileD, FileE, FileF, FileG, FileH
}

public enum Rank : byte
{
    Rank1, Rank2, Rank3, Rank4, Rank5, Rank6, Rank7, Rank8
}

public static class SquareExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static File GetFile(this Square square)
    {
        return (File)((byte)square & 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rank GetRank(this Square square)
    {
        return (Rank)((byte)square >> 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square MakeSquare(File file, Rank rank)
    {
        return (Square)((byte)rank * 8 + (byte)file);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this Square square)
    {
        return square <= Square.H8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToBitboard(this Square square)
    {
        return 1UL << (byte)square;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Distance(Square s1, Square s2)
    {
        int file1 = (int)s1 & 7;
        int rank1 = (int)s1 >> 3;
        int file2 = (int)s2 & 7;
        int rank2 = (int)s2 >> 3;
        return Math.Max(Math.Abs(file1 - file2), Math.Abs(rank1 - rank2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ManhattanDistance(Square s1, Square s2)
    {
        int file1 = (int)s1 & 7;
        int rank1 = (int)s1 >> 3;
        int file2 = (int)s2 & 7;
        int rank2 = (int)s2 >> 3;
        return Math.Abs(file1 - file2) + Math.Abs(rank1 - rank2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square Mirror(this Square square)
    {
        return (Square)((int)square ^ 56);
    }
}