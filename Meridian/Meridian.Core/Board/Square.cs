#nullable enable

using System.Runtime.CompilerServices;

namespace Meridian.Core.Board;

public enum Square
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

public static class SquareExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int File(this Square square) => (int)square & 7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Rank(this Square square) => (int)square >> 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square FromFileRank(int file, int rank) => (Square)((rank << 3) | file);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this Square square) => square >= Square.A1 && square <= Square.H8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard ToBitboard(this Square square) => new(1UL << (int)square);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Square Mirror(this Square square) => (Square)((int)square ^ 56);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLight(this Square square)
    {
        var file = square.File();
        var rank = square.Rank();
        return ((file + rank) & 1) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDark(this Square square) => !square.IsLight();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Distance(this Square from, Square to)
    {
        var fileDist = Math.Abs(from.File() - to.File());
        var rankDist = Math.Abs(from.Rank() - to.Rank());
        return Math.Max(fileDist, rankDist);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ManhattanDistance(this Square from, Square to)
    {
        var fileDist = Math.Abs(from.File() - to.File());
        var rankDist = Math.Abs(from.Rank() - to.Rank());
        return fileDist + rankDist;
    }

    public static string ToAlgebraic(this Square square)
    {
        if (!square.IsValid()) return "-";
        var file = (char)('a' + square.File());
        var rank = (char)('1' + square.Rank());
        return $"{file}{rank}";
    }

    public static Square ParseSquare(string algebraic)
    {
        if (string.IsNullOrEmpty(algebraic) || algebraic.Length != 2)
            return Square.None;

        var file = algebraic[0] - 'a';
        var rank = algebraic[1] - '1';

        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            return Square.None;

        return FromFileRank(file, rank);
    }
}