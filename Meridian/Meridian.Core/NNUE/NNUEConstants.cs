namespace Meridian.Core.NNUE;

public static class NNUEConstants
{
    public const int InputDimensions = 768;
    public const int L1Size = 1536;
    public const int L2Size = 16;
    public const int L3Size = 32;
    public const int OutputBuckets = 8;
    
    public const int NetworkScale = 400;
    public const int QA = 255;
    public const int QB = 128;
    
    public const int KingBuckets = 13;
    public const int PieceTypes = 6;
    public const int Colors = 2;
    
    public const int MaxPieces = 32;
    
    public static int FeatureWeightIndex(int piece, int square, int kingSquare, int perspective)
    {
        // Obsidian's indexing: [KingBucket][color != perspective][pieceType][square][L1]
        // piece = 0-11 (6 piece types * 2 colors)
        int pieceType = piece % 6;
        int pieceColor = piece / 6;
        int colorIndex = (pieceColor != perspective) ? 1 : 0;
        
        // Apply perspective transformation
        int relativeSquare = square;
        int relativeKingSquare = kingSquare;
        
        if (perspective == 1) // Black perspective
        {
            relativeSquare ^= 56; // Vertical flip
            relativeKingSquare ^= 56;
        }
        
        // Obsidian uses a different king orientation check
        // If king is on files e-h (4-7), we mirror horizontally
        if ((relativeKingSquare & 0x07) >= 4) // Check file (0-7)
        {
            relativeSquare ^= 7; // Horizontal flip
            relativeKingSquare ^= 7;
        }
        
        // Calculate base index into the weight array
        return (KingBucket(relativeKingSquare) * 2 * 6 * 64 + 
                colorIndex * 6 * 64 + 
                pieceType * 64 + 
                relativeSquare) * L1Size;
    }
    
    private static readonly int[] KingBucketsScheme = 
    {
        0, 1, 2, 3, 3, 2, 1, 0,
        4, 5, 6, 7, 7, 6, 5, 4,
        8, 8, 9, 9, 9, 9, 8, 8,
        10, 10, 10, 10, 10, 10, 10, 10,
        11, 11, 11, 11, 11, 11, 11, 11, 
        11, 11, 11, 11, 11, 11, 11, 11, 
        12, 12, 12, 12, 12, 12, 12, 12, 
        12, 12, 12, 12, 12, 12, 12, 12, 
    };
    
    public static int KingBucket(int kingSquare)
    {
        return KingBucketsScheme[kingSquare & 63];
    }
}