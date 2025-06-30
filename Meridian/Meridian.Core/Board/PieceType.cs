#nullable enable

namespace Meridian.Core.Board;

using System.Runtime.CompilerServices;

public enum PieceType
{
    None,
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

public enum Piece
{
    None = 0,
    WhitePawn = 1,
    WhiteKnight = 2,
    WhiteBishop = 3,
    WhiteRook = 4,
    WhiteQueen = 5,
    WhiteKing = 6,
    BlackPawn = 9,
    BlackKnight = 10,
    BlackBishop = 11,
    BlackRook = 12,
    BlackQueen = 13,
    BlackKing = 14
}

public enum Color
{
    White,
    Black
}

public static class ColorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color Opponent(this Color color)
    {
        return (Color)(1 - (int)color);
    }
}

public static class PieceExtensions
{
    public static PieceType Type(this Piece piece) => (PieceType)((int)piece & 7);

    public static Color GetColor(this Piece piece)
    {
        if (piece == Piece.None)
        {
            return Color.White;
        }

        return (Color)((int)piece >> 3);
    }

    public static Piece MakePiece(Color color, PieceType type) => (Piece)((int)type | ((int)color << 3));
}
