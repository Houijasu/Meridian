namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class Zobrist
{
    // Zobrist keys for each piece on each square
    private static readonly ulong[,] PieceKeys = new ulong[12, 64]; // 6 piece types * 2 colors * 64 squares
    private static readonly ulong[] CastlingKeys = new ulong[16]; // All castling right combinations
    private static readonly ulong[] EnPassantKeys = new ulong[8]; // One for each file
    private static readonly ulong SideToMoveKey;
    
    static Zobrist()
    {
        // Initialize with deterministic pseudo-random numbers
        var rng = new Random(1337); // Fixed seed for reproducibility
        
        // Initialize piece keys
        for (int i = 0; i < 12; i++)
        {
            for (int j = 0; j < 64; j++)
            {
                PieceKeys[i, j] = RandomUlong(rng);
            }
        }
        
        // Initialize castling keys
        for (int i = 0; i < 16; i++)
        {
            CastlingKeys[i] = RandomUlong(rng);
        }
        
        // Initialize en passant keys
        for (int i = 0; i < 8; i++)
        {
            EnPassantKeys[i] = RandomUlong(rng);
        }
        
        // Initialize side to move key
        SideToMoveKey = RandomUlong(rng);
    }
    
    private static ulong RandomUlong(Random rng)
    {
        byte[] buffer = new byte[8];
        rng.NextBytes(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ComputeHash(ref BoardState board)
    {
        ulong hash = 0;
        
        // Hash pieces
        ulong occupied = board.AllPieces;
        while (occupied != 0)
        {
            int sq = Bitboard.PopLsb(ref occupied);
            var (piece, color) = board.GetPieceAt((Square)sq);
            
            if (piece != Piece.None)
            {
                int pieceIndex = GetPieceIndex(piece, color);
                hash ^= PieceKeys[pieceIndex, sq];
            }
        }
        
        // Hash castling rights
        hash ^= CastlingKeys[(int)board.CastlingRights];
        
        // Hash en passant
        if (board.EnPassantSquare != Square.None)
        {
            int file = (int)board.EnPassantSquare % 8;
            hash ^= EnPassantKeys[file];
        }
        
        // Hash side to move
        if (board.SideToMove == Color.Black)
        {
            hash ^= SideToMoveKey;
        }
        
        return hash;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong UpdateHash(ulong hash, Move move, ref BoardState board)
    {
        // This method updates hash incrementally after a move
        // For now, we'll recompute (can optimize later)
        BoardState newBoard = board;
        newBoard.MakeMove(move);
        return ComputeHash(ref newBoard);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceIndex(Piece piece, Color color)
    {
        // Map piece and color to index 0-11
        int index = (int)piece - 1; // Piece.Pawn = 1, so subtract 1
        if (color == Color.Black)
            index += 6;
        return index;
    }
    
    // Methods for incremental hash updates (optimization)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong TogglePiece(ulong hash, Piece piece, Color color, Square square)
    {
        int pieceIndex = GetPieceIndex(piece, color);
        return hash ^ PieceKeys[pieceIndex, (int)square];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToggleCastling(ulong hash, CastlingRights oldRights, CastlingRights newRights)
    {
        return hash ^ CastlingKeys[(int)oldRights] ^ CastlingKeys[(int)newRights];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToggleEnPassant(ulong hash, Square oldEp, Square newEp)
    {
        if (oldEp != Square.None)
            hash ^= EnPassantKeys[(int)oldEp % 8];
        if (newEp != Square.None)
            hash ^= EnPassantKeys[(int)newEp % 8];
        return hash;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToggleSideToMove(ulong hash)
    {
        return hash ^ SideToMoveKey;
    }
}