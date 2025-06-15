namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public ref struct BoardState
{
    public ulong WhitePawns;
    public ulong WhiteKnights;
    public ulong WhiteBishops;
    public ulong WhiteRooks;
    public ulong WhiteQueens;
    public ulong WhiteKing;
    
    public ulong BlackPawns;
    public ulong BlackKnights;
    public ulong BlackBishops;
    public ulong BlackRooks;
    public ulong BlackQueens;
    public ulong BlackKing;
    
    public Color SideToMove;
    public CastlingRights CastlingRights;
    public Square EnPassantSquare;
    public byte HalfMoveClock;
    public ushort FullMoveNumber;
    public ulong Hash;
    public int CachedMaterial; // Cached material value for NNUE
    
    public ulong WhitePieces
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => WhitePawns | WhiteKnights | WhiteBishops | WhiteRooks | WhiteQueens | WhiteKing;
    }
    
    public ulong BlackPieces
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BlackPawns | BlackKnights | BlackBishops | BlackRooks | BlackQueens | BlackKing;
    }
    
    public ulong AllPieces
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => WhitePieces | BlackPieces;
    }
    
    public ulong EmptySquares
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ~AllPieces;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong GetPieceBitboard(Piece piece, Color color)
    {
        return (piece, color) switch
        {
            (Piece.Pawn, Color.White) => WhitePawns,
            (Piece.Knight, Color.White) => WhiteKnights,
            (Piece.Bishop, Color.White) => WhiteBishops,
            (Piece.Rook, Color.White) => WhiteRooks,
            (Piece.Queen, Color.White) => WhiteQueens,
            (Piece.King, Color.White) => WhiteKing,
            (Piece.Pawn, Color.Black) => BlackPawns,
            (Piece.Knight, Color.Black) => BlackKnights,
            (Piece.Bishop, Color.Black) => BlackBishops,
            (Piece.Rook, Color.Black) => BlackRooks,
            (Piece.Queen, Color.Black) => BlackQueens,
            (Piece.King, Color.Black) => BlackKing,
            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPieceBitboard(Piece piece, Color color, ulong bitboard)
    {
        switch ((piece, color))
        {
            case (Piece.Pawn, Color.White): WhitePawns = bitboard; break;
            case (Piece.Knight, Color.White): WhiteKnights = bitboard; break;
            case (Piece.Bishop, Color.White): WhiteBishops = bitboard; break;
            case (Piece.Rook, Color.White): WhiteRooks = bitboard; break;
            case (Piece.Queen, Color.White): WhiteQueens = bitboard; break;
            case (Piece.King, Color.White): WhiteKing = bitboard; break;
            case (Piece.Pawn, Color.Black): BlackPawns = bitboard; break;
            case (Piece.Knight, Color.Black): BlackKnights = bitboard; break;
            case (Piece.Bishop, Color.Black): BlackBishops = bitboard; break;
            case (Piece.Rook, Color.Black): BlackRooks = bitboard; break;
            case (Piece.Queen, Color.Black): BlackQueens = bitboard; break;
            case (Piece.King, Color.Black): BlackKing = bitboard; break;
        }
    }

    /// <summary>
    /// Calculate material value (for NNUE output bucket selection)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateMaterial()
    {
        // PlentyChess material values
        const int PawnValue = 94;
        const int KnightValue = 281;
        const int BishopValue = 297;
        const int RookValue = 512;
        const int QueenValue = 936;
        
        int material = 0;
        
        // Count pieces
        material += Bitboard.PopCount(WhitePawns | BlackPawns) * PawnValue;
        material += Bitboard.PopCount(WhiteKnights | BlackKnights) * KnightValue;
        material += Bitboard.PopCount(WhiteBishops | BlackBishops) * BishopValue;
        material += Bitboard.PopCount(WhiteRooks | BlackRooks) * RookValue;
        material += Bitboard.PopCount(WhiteQueens | BlackQueens) * QueenValue;
        
        return material;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly (Piece piece, Color color) GetPieceAt(Square square)
    {
        ulong bit = square.ToBitboard();
        
        if ((WhitePawns & bit) != 0) return (Piece.Pawn, Color.White);
        if ((WhiteKnights & bit) != 0) return (Piece.Knight, Color.White);
        if ((WhiteBishops & bit) != 0) return (Piece.Bishop, Color.White);
        if ((WhiteRooks & bit) != 0) return (Piece.Rook, Color.White);
        if ((WhiteQueens & bit) != 0) return (Piece.Queen, Color.White);
        if ((WhiteKing & bit) != 0) return (Piece.King, Color.White);
        
        if ((BlackPawns & bit) != 0) return (Piece.Pawn, Color.Black);
        if ((BlackKnights & bit) != 0) return (Piece.Knight, Color.Black);
        if ((BlackBishops & bit) != 0) return (Piece.Bishop, Color.Black);
        if ((BlackRooks & bit) != 0) return (Piece.Rook, Color.Black);
        if ((BlackQueens & bit) != 0) return (Piece.Queen, Color.Black);
        if ((BlackKing & bit) != 0) return (Piece.King, Color.Black);
        
        return (Piece.None, Color.White);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddPiece(Square square, Piece piece, Color color)
    {
        ulong bit = square.ToBitboard();
        ulong current = GetPieceBitboard(piece, color);
        SetPieceBitboard(piece, color, current | bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemovePiece(Square square, Piece piece, Color color)
    {
        ulong bit = square.ToBitboard();
        ulong current = GetPieceBitboard(piece, color);
        SetPieceBitboard(piece, color, current & ~bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MovePiece(Square from, Square to, Piece piece, Color color)
    {
        ulong fromToBit = from.ToBitboard() | to.ToBitboard();
        ulong current = GetPieceBitboard(piece, color);
        SetPieceBitboard(piece, color, current ^ fromToBit);
    }

    public static BoardState StartingPosition()
    {
        var board = new BoardState
        {
            WhitePawns = 0x000000000000FF00UL,
            WhiteKnights = 0x0000000000000042UL,
            WhiteBishops = 0x0000000000000024UL,
            WhiteRooks = 0x0000000000000081UL,
            WhiteQueens = 0x0000000000000008UL,
            WhiteKing = 0x0000000000000010UL,
            
            BlackPawns = 0x00FF000000000000UL,
            BlackKnights = 0x4200000000000000UL,
            BlackBishops = 0x2400000000000000UL,
            BlackRooks = 0x8100000000000000UL,
            BlackQueens = 0x0800000000000000UL,
            BlackKing = 0x1000000000000000UL,
            
            SideToMove = Color.White,
            CastlingRights = CastlingRights.All,
            EnPassantSquare = Square.None,
            HalfMoveClock = 0,
            FullMoveNumber = 1,
            Hash = 0,
            CachedMaterial = 0
        };
        
        // Calculate initial material
        board.CachedMaterial = board.CalculateMaterial();
        
        return board;
    }
}

public enum Color : byte
{
    White = 0,
    Black = 1
}

public enum Piece : byte
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6
}

[Flags]
public enum CastlingRights : byte
{
    None = 0,
    WhiteKingSide = 1,
    WhiteQueenSide = 2,
    BlackKingSide = 4,
    BlackQueenSide = 8,
    White = WhiteKingSide | WhiteQueenSide,
    Black = BlackKingSide | BlackQueenSide,
    All = White | Black
}

public static class ColorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color Opposite(this Color color)
    {
        return (Color)(1 - (byte)color);
    }
}