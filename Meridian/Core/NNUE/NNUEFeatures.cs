namespace Meridian.Core.NNUE;

using System.Runtime.CompilerServices;

/// <summary>
/// NNUE feature extraction for HalfKAv2_hm architecture
/// Input features: 768 (64 squares × 6 piece types × 2 colors)
/// </summary>
public static class NNUEFeatures
{
    // Feature dimensions
    public const int NumSquares = 64;
    public const int NumPieceTypes = 6; // Pawn, Knight, Bishop, Rook, Queen, King
    public const int NumColors = 2;
    public const int InputDimensions = NumSquares * NumPieceTypes * NumColors; // 768
    
    // Maximum pieces on board (for active features)
    public const int MaxActivePieces = 32;
    
    /// <summary>
    /// Extract active feature indices from board position
    /// Returns indices of non-zero inputs (max 32 in legal position)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExtractFeatures(ref BoardState board, Span<int> whiteFeatures, Span<int> blackFeatures)
    {
        int featureCount = 0;
        
        // Extract features for all pieces
        ExtractPieceFeatures(ref board, board.WhitePawns, Piece.Pawn, Color.White, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.WhiteKnights, Piece.Knight, Color.White, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.WhiteBishops, Piece.Bishop, Color.White, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.WhiteRooks, Piece.Rook, Color.White, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.WhiteQueens, Piece.Queen, Color.White, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.WhiteKing, Piece.King, Color.White, ref featureCount, whiteFeatures, blackFeatures);
        
        ExtractPieceFeatures(ref board, board.BlackPawns, Piece.Pawn, Color.Black, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.BlackKnights, Piece.Knight, Color.Black, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.BlackBishops, Piece.Bishop, Color.Black, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.BlackRooks, Piece.Rook, Color.Black, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.BlackQueens, Piece.Queen, Color.Black, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPieceFeatures(ref board, board.BlackKing, Piece.King, Color.Black, ref featureCount, whiteFeatures, blackFeatures);
        
        return featureCount;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractPieceFeatures(
        ref BoardState board, 
        ulong pieces, 
        Piece pieceType, 
        Color pieceColor,
        ref int featureCount,
        Span<int> whiteFeatures,
        Span<int> blackFeatures)
    {
        while (pieces != 0)
        {
            int square = Bitboard.PopLsb(ref pieces);
            
            // Calculate feature indices for both perspectives
            int whiteFeatureIndex = GetFeatureIndex(square, pieceType, pieceColor);
            int blackFeatureIndex = GetFeatureIndex(MirrorSquare(square), pieceType, pieceColor.Opposite());
            
            // Add to feature lists
            whiteFeatures[featureCount] = whiteFeatureIndex;
            blackFeatures[featureCount] = blackFeatureIndex;
            featureCount++;
        }
    }
    
    /// <summary>
    /// Calculate feature index for (square, piece_type, color) tuple
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetFeatureIndex(int square, Piece pieceType, Color pieceColor)
    {
        // Layout: [White pieces on 64 squares][Black pieces on 64 squares]
        // Within each color block: [Pawns][Knights][Bishops][Rooks][Queens][Kings]
        int pieceTypeIndex = (int)pieceType - 1; // 0-5
        int colorOffset = pieceColor == Color.White ? 0 : NumSquares * NumPieceTypes;
        return colorOffset + pieceTypeIndex * NumSquares + square;
    }
    
    /// <summary>
    /// Mirror square vertically (for black's perspective)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MirrorSquare(int square)
    {
        return square ^ 56; // Flip rank (vertical mirror)
    }
    
    /// <summary>
    /// Get changed features when making a move (for incremental updates)
    /// </summary>
    public static void GetChangedFeatures(
        ref BoardState board,
        Move move,
        Span<int> removedWhite,
        Span<int> removedBlack,
        Span<int> addedWhite,
        Span<int> addedBlack,
        out int numRemoved,
        out int numAdded)
    {
        numRemoved = 0;
        numAdded = 0;
        
        // Get piece info
        Piece movingPiece = board.GetPieceType(move.From);
        Color movingColor = board.GetPieceColor(move.From);
        
        // Remove piece from source square
        removedWhite[numRemoved] = GetFeatureIndex((int)move.From, movingPiece, movingColor);
        removedBlack[numRemoved] = GetFeatureIndex(MirrorSquare((int)move.From), movingPiece, movingColor.Opposite());
        numRemoved++;
        
        // Handle captures
        if (move.IsCapture())
        {
            Piece capturedPiece = board.GetPieceType(move.To);
            Color capturedColor = movingColor.Opposite();
            
            removedWhite[numRemoved] = GetFeatureIndex((int)move.To, capturedPiece, capturedColor);
            removedBlack[numRemoved] = GetFeatureIndex(MirrorSquare((int)move.To), capturedPiece, capturedColor.Opposite());
            numRemoved++;
        }
        
        // Add piece to destination square (or promotion piece)
        Piece finalPiece = move.IsPromotion() ? move.PromotionPiece : movingPiece;
        addedWhite[numAdded] = GetFeatureIndex((int)move.To, finalPiece, movingColor);
        addedBlack[numAdded] = GetFeatureIndex(MirrorSquare((int)move.To), finalPiece, movingColor.Opposite());
        numAdded++;
        
        // Handle castling (move rook)
        if (move.IsCastle())
        {
            int rookFrom, rookTo;
            if (move.To == Square.G1 || move.To == Square.G8) // Kingside
            {
                rookFrom = (int)move.To + 1;
                rookTo = (int)move.To - 1;
            }
            else // Queenside
            {
                rookFrom = (int)move.To - 2;
                rookTo = (int)move.To + 1;
            }
            
            removedWhite[numRemoved] = GetFeatureIndex(rookFrom, Piece.Rook, movingColor);
            removedBlack[numRemoved] = GetFeatureIndex(MirrorSquare(rookFrom), Piece.Rook, movingColor.Opposite());
            numRemoved++;
            
            addedWhite[numAdded] = GetFeatureIndex(rookTo, Piece.Rook, movingColor);
            addedBlack[numAdded] = GetFeatureIndex(MirrorSquare(rookTo), Piece.Rook, movingColor.Opposite());
            numAdded++;
        }
        
        // Handle en passant (remove captured pawn)
        if (move.IsEnPassant())
        {
            int capturedPawnSquare = (int)move.To + (movingColor == Color.White ? -8 : 8);
            removedWhite[numRemoved] = GetFeatureIndex(capturedPawnSquare, Piece.Pawn, movingColor.Opposite());
            removedBlack[numRemoved] = GetFeatureIndex(MirrorSquare(capturedPawnSquare), Piece.Pawn, movingColor);
            numRemoved++;
        }
    }
}

// Extension methods for BoardState to support NNUE
public static class BoardStateNNUEExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Piece GetPieceType(this ref BoardState board, Square square)
    {
        ulong bb = 1UL << (int)square;
        
        if (((board.WhitePawns | board.BlackPawns) & bb) != 0) return Piece.Pawn;
        if (((board.WhiteKnights | board.BlackKnights) & bb) != 0) return Piece.Knight;
        if (((board.WhiteBishops | board.BlackBishops) & bb) != 0) return Piece.Bishop;
        if (((board.WhiteRooks | board.BlackRooks) & bb) != 0) return Piece.Rook;
        if (((board.WhiteQueens | board.BlackQueens) & bb) != 0) return Piece.Queen;
        if (((board.WhiteKing | board.BlackKing) & bb) != 0) return Piece.King;
        
        return Piece.None;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color GetPieceColor(this ref BoardState board, Square square)
    {
        ulong bb = 1UL << (int)square;
        return (board.WhitePieces & bb) != 0 ? Color.White : Color.Black;
    }
}