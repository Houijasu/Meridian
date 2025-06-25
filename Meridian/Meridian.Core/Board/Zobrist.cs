#nullable enable

using System.Runtime.CompilerServices;

namespace Meridian.Core.Board;

public static class Zobrist
{
    private static readonly ulong[,] s_pieceKeys = new ulong[64, 16];
    private static readonly ulong[] s_castlingKeys = new ulong[16];
    private static readonly ulong[] s_enPassantKeys = new ulong[8];
    private static readonly ulong s_sideKey;
    
    static Zobrist()
    {
        var rng = new Random(0x1337BEEF);
        
        for (var square = 0; square < 64; square++)
        {
            for (var piece = 0; piece < 16; piece++)
            {
                s_pieceKeys[square, piece] = NextRandom(rng);
            }
        }
        
        for (var i = 0; i < 16; i++)
        {
            s_castlingKeys[i] = NextRandom(rng);
        }
        
        for (var i = 0; i < 8; i++)
        {
            s_enPassantKeys[i] = NextRandom(rng);
        }
        
        s_sideKey = NextRandom(rng);
    }
    
    private static ulong NextRandom(Random rng)
    {
        var bytes = new byte[8];
        rng.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong PieceKey(Square square, Piece piece) => s_pieceKeys[(int)square, (int)piece];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CastlingKey(CastlingRights rights) => s_castlingKeys[(int)rights];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong EnPassantKey(Square square) => 
        square == Square.None ? 0 : s_enPassantKeys[square.File()];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong SideKey() => s_sideKey;
    
    public static ulong ComputeKey(Position position)
    {
        if (position == null) return 0;
        
        ulong key = 0;
        
        for (var square = Square.A1; square <= Square.H8; square++)
        {
            var piece = position.GetPiece(square);
            if (piece != Piece.None)
            {
                key ^= PieceKey(square, piece);
            }
        }
        
        key ^= CastlingKey(position.CastlingRights);
        key ^= EnPassantKey(position.EnPassantSquare);
        
        if (position.SideToMove == Color.Black)
        {
            key ^= SideKey();
        }
        
        return key;
    }
}