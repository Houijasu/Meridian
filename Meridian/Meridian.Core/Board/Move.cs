#nullable enable

using System.Runtime.CompilerServices;

namespace Meridian.Core.Board;

[Flags]
public enum MoveType
{
    None = 0,
    Capture = 1 << 0,
    DoublePush = 1 << 1,
    EnPassant = 1 << 2,
    Castling = 1 << 3,
    Promotion = 1 << 4,
    Check = 1 << 5,
    Checkmate = 1 << 6
}

public readonly struct Move : IEquatable<Move>
{
    private readonly uint _data;

    public static readonly Move None = new(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Move(Square from, Square to, MoveType flags = MoveType.None, 
                Piece captured = Piece.None, PieceType promotion = PieceType.None)
    {
        _data = (uint)from |
                ((uint)to << 6) |
                ((uint)flags << 12) |
                ((uint)captured << 20) |
                ((uint)promotion << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Move(uint data) => _data = data;

    public Square From => (Square)(_data & 0x3F);
    public Square To => (Square)((_data >> 6) & 0x3F);
    public MoveType Flags => (MoveType)((_data >> 12) & 0xFF);
    public Piece CapturedPiece => (Piece)((_data >> 20) & 0xF);
    public PieceType PromotionType => (PieceType)((_data >> 24) & 0x7);

    public bool IsCapture => (Flags & MoveType.Capture) != 0;
    public bool IsPromotion => (Flags & MoveType.Promotion) != 0;
    public bool IsCastling => (Flags & MoveType.Castling) != 0;
    public bool IsEnPassant => (Flags & MoveType.EnPassant) != 0;
    public bool IsDoublePush => (Flags & MoveType.DoublePush) != 0;
    public bool IsQuiet => !IsCapture && !IsPromotion;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Move left, Move right) => left._data == right._data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Move left, Move right) => left._data != right._data;

    public bool Equals(Move other) => _data == other._data;

    public override bool Equals(object? obj) => obj is Move other && Equals(other);

    public override int GetHashCode() => _data.GetHashCode();

    public string ToUci()
    {
        if (this == None) return "0000";
        
        var result = From.ToAlgebraic() + To.ToAlgebraic();
        
        if (IsPromotion)
        {
            result += PromotionType switch
            {
                PieceType.Queen => "q",
                PieceType.Rook => "r",
                PieceType.Bishop => "b",
                PieceType.Knight => "n",
                _ => ""
            };
        }
        
        return result;
    }

    public override string ToString() => ToUci();
}