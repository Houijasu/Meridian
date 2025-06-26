#nullable enable

namespace Meridian.Core.Board;

/// <summary>
/// Stores information needed to undo a move
/// </summary>
public readonly struct UndoInfo : IEquatable<UndoInfo>
{
    public Piece CapturedPiece { get; }
    public CastlingRights CastlingRights { get; }
    public Square EnPassantSquare { get; }
    public int HalfmoveClock { get; }
    public ulong ZobristKey { get; }
    
    public UndoInfo(Piece capturedPiece, CastlingRights castlingRights, 
                    Square enPassantSquare, int halfmoveClock, ulong zobristKey)
    {
        CapturedPiece = capturedPiece;
        CastlingRights = castlingRights;
        EnPassantSquare = enPassantSquare;
        HalfmoveClock = halfmoveClock;
        ZobristKey = zobristKey;
    }
    
    public bool Equals(UndoInfo other)
    {
        return CapturedPiece == other.CapturedPiece &&
               CastlingRights == other.CastlingRights &&
               EnPassantSquare == other.EnPassantSquare &&
               HalfmoveClock == other.HalfmoveClock &&
               ZobristKey == other.ZobristKey;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is UndoInfo other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(CapturedPiece, CastlingRights, EnPassantSquare, HalfmoveClock, ZobristKey);
    }
    
    public static bool operator ==(UndoInfo left, UndoInfo right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(UndoInfo left, UndoInfo right)
    {
        return !left.Equals(right);
    }
}