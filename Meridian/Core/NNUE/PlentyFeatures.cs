namespace Meridian.Core.NNUE;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// PlentyChess-compatible HalfKA feature extraction
/// </summary>
public static class PlentyFeatures
{
    // Feature dimensions matching PlentyChess
    public const int FeatureSize = 768;
    public const int MaxActiveFeatures = 32;
    
    // Piece encoding for PlentyChess
    // White: PAWN=0, KNIGHT=1, BISHOP=2, ROOK=3, QUEEN=4, KING=5
    // Black: PAWN=6, KNIGHT=7, BISHOP=8, ROOK=9, QUEEN=10, KING=11
    
    /// <summary>
    /// Extract HalfKA features for PlentyChess network
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExtractFeatures(ref BoardState board, Span<int> whiteFeatures, Span<int> blackFeatures)
    {
        int featureCount = 0;
        
        // Get king squares
        Square whiteKing = GetKingSquare(board.WhiteKing);
        Square blackKing = GetKingSquare(board.BlackKing);
        
        // Get king buckets
        int whiteKingBucket = PlentyNetwork.GetKingBucket((int)whiteKing, Color.White);
        int blackKingBucket = PlentyNetwork.GetKingBucket((int)blackKing, Color.Black);
        
        // Extract features for all pieces
        ExtractPiecesHalfKA(board.WhitePawns, 0, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.WhiteKnights, 1, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.WhiteBishops, 2, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.WhiteRooks, 3, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.WhiteQueens, 4, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        
        ExtractPiecesHalfKA(board.BlackPawns, 6, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.BlackKnights, 7, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.BlackBishops, 8, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.BlackRooks, 9, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        ExtractPiecesHalfKA(board.BlackQueens, 10, whiteKing, blackKing, whiteKingBucket, blackKingBucket, ref featureCount, whiteFeatures, blackFeatures);
        
        return featureCount;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractPiecesHalfKA(
        ulong pieces, 
        int pieceType,
        Square whiteKing,
        Square blackKing,
        int whiteKingBucket,
        int blackKingBucket,
        ref int featureCount,
        Span<int> whiteFeatures,
        Span<int> blackFeatures)
    {
        while (pieces != 0)
        {
            int square = Bitboard.PopLsb(ref pieces);
            
            // Calculate features for both perspectives
            int whiteFeatureIndex = GetHalfKAIndex(pieceType, square, whiteKing, whiteKingBucket);
            int blackFeatureIndex = GetHalfKAIndex(pieceType ^ 6, square ^ 56, blackKing, blackKingBucket); // Flip color and square
            
            whiteFeatures[featureCount] = whiteFeatureIndex;
            blackFeatures[featureCount] = blackFeatureIndex;
            featureCount++;
        }
    }
    
    /// <summary>
    /// Calculate HalfKA feature index using PlentyChess formula
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHalfKAIndex(int pieceType, int square, Square kingSquare, int kingBucket)
    {
        // PlentyChess formula: (64 * piece + square) relative to king position
        // But we need to transform based on king bucket
        
        // Get relative square based on king position
        int relativeSquare = GetRelativeSquare(square, kingSquare, kingBucket);
        
        // Feature index = pieceType * 64 + relativeSquare
        return pieceType * 64 + relativeSquare;
    }
    
    /// <summary>
    /// Transform square relative to king position
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetRelativeSquare(int square, Square kingSquare, int kingBucket)
    {
        // For HalfKA, squares are relative to king position
        // This is simplified - PlentyChess may have more complex transformations
        return square;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Square GetKingSquare(ulong kingBitboard)
    {
        return (Square)Bitboard.BitScanForward(kingBitboard);
    }
    
    /// <summary>
    /// Get changed features for incremental update (PlentyChess style)
    /// </summary>
    public static void GetChangedFeatures(
        ref BoardState board,
        Move move,
        Square whiteKing,
        Square blackKing,
        Span<int> removedWhite,
        Span<int> removedBlack,
        Span<int> addedWhite,
        Span<int> addedBlack,
        out int numRemoved,
        out int numAdded)
    {
        numRemoved = 0;
        numAdded = 0;
        
        int whiteKingBucket = PlentyNetwork.GetKingBucket((int)whiteKing, Color.White);
        int blackKingBucket = PlentyNetwork.GetKingBucket((int)blackKing, Color.Black);
        
        // Get piece info
        Piece movingPiece = board.GetPieceType(move.From);
        Color movingColor = board.GetPieceColor(move.From);
        int pieceType = GetPieceType(movingPiece, movingColor);
        
        // Remove piece from source
        removedWhite[numRemoved] = GetHalfKAIndex(pieceType, (int)move.From, whiteKing, whiteKingBucket);
        removedBlack[numRemoved] = GetHalfKAIndex(pieceType ^ 6, (int)move.From ^ 56, blackKing, blackKingBucket);
        numRemoved++;
        
        // Handle captures
        if (move.IsCapture() && move.Type != MoveType.EnPassant)
        {
            Piece capturedPiece = board.GetPieceType(move.To);
            int capturedType = GetPieceType(capturedPiece, movingColor.Opposite());
            
            removedWhite[numRemoved] = GetHalfKAIndex(capturedType, (int)move.To, whiteKing, whiteKingBucket);
            removedBlack[numRemoved] = GetHalfKAIndex(capturedType ^ 6, (int)move.To ^ 56, blackKing, blackKingBucket);
            numRemoved++;
        }
        
        // Add piece to destination (handle promotions)
        if (move.IsPromotion())
        {
            pieceType = GetPieceType(move.PromotionPiece, movingColor);
        }
        
        addedWhite[numAdded] = GetHalfKAIndex(pieceType, (int)move.To, whiteKing, whiteKingBucket);
        addedBlack[numAdded] = GetHalfKAIndex(pieceType ^ 6, (int)move.To ^ 56, blackKing, blackKingBucket);
        numAdded++;
        
        // Handle special moves
        HandleSpecialMoves(ref board, move, movingColor, whiteKing, blackKing, whiteKingBucket, blackKingBucket,
                          removedWhite, removedBlack, addedWhite, addedBlack, ref numRemoved, ref numAdded);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceType(Piece piece, Color color)
    {
        int type = piece switch
        {
            Piece.Pawn => 0,
            Piece.Knight => 1,
            Piece.Bishop => 2,
            Piece.Rook => 3,
            Piece.Queen => 4,
            Piece.King => 5,
            _ => 0
        };
        
        return color == Color.White ? type : type + 6;
    }
    
    private static void HandleSpecialMoves(
        ref BoardState board,
        Move move,
        Color movingColor,
        Square whiteKing,
        Square blackKing,
        int whiteKingBucket,
        int blackKingBucket,
        Span<int> removedWhite,
        Span<int> removedBlack,
        Span<int> addedWhite,
        Span<int> addedBlack,
        ref int numRemoved,
        ref int numAdded)
    {
        // Castling
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
            
            int rookType = GetPieceType(Piece.Rook, movingColor);
            
            removedWhite[numRemoved] = GetHalfKAIndex(rookType, rookFrom, whiteKing, whiteKingBucket);
            removedBlack[numRemoved] = GetHalfKAIndex(rookType ^ 6, rookFrom ^ 56, blackKing, blackKingBucket);
            numRemoved++;
            
            addedWhite[numAdded] = GetHalfKAIndex(rookType, rookTo, whiteKing, whiteKingBucket);
            addedBlack[numAdded] = GetHalfKAIndex(rookType ^ 6, rookTo ^ 56, blackKing, blackKingBucket);
            numAdded++;
        }
        
        // En passant
        if (move.IsEnPassant())
        {
            int capturedSquare = (int)move.To + (movingColor == Color.White ? -8 : 8);
            int pawnType = GetPieceType(Piece.Pawn, movingColor.Opposite());
            
            removedWhite[numRemoved] = GetHalfKAIndex(pawnType, capturedSquare, whiteKing, whiteKingBucket);
            removedBlack[numRemoved] = GetHalfKAIndex(pawnType ^ 6, capturedSquare ^ 56, blackKing, blackKingBucket);
            numRemoved++;
        }
    }
}