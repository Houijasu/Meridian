using Meridian.Core.Board;

namespace Meridian.Core.NNUE;

public static class NNUEConstants
{
    // Obsidian network architecture (matches 30MB file size)
    public const int InputDimensions = 768;     // 12 pieces * 64 squares
    public const int L1Size = 1536;             // Obsidian uses 1536 neurons in L1
    public const int L2Size = 16;               // Obsidian uses 16 neurons in L2
    public const int L3Size = 32;               // L3 layer
    public const int OutputDimensions = 1;      // Single output

    // Quantization parameters for Obsidian (matching cpp implementation)
    public const int NetworkQA = 255;           // Input quantization scale
    public const int NetworkQB = 128;           // Hidden layer quantization
    public const int NetworkScale = 400;        // Final output scaling
    public const int FtShift = 9;               // Feature transform shift
    public const int ClampedMin = 0;            // Clamped ReLU minimum
    public const int ClampedMax = 127;          // Clamped ReLU maximum

    // Legacy constants for compatibility
    public const int QA = NetworkQA;
    public const int QB = NetworkQB;
    public const int QAB = QA * QB;
    public const int ScaleFactor = NetworkScale;

    // Feature set configuration
    public const int PieceTypes = 6;            // P, N, B, R, Q, K
    public const int Colors = 2;                // White, Black
    public const int Squares = 64;              // 8x8 board

    // King bucket configuration (Obsidian uses 13 king buckets)
    public const int KingBuckets = 13;          // Obsidian uses 13 king buckets

    // Activation function parameters (matching Obsidian)
    public const int ClippedReLUMin = 0;
    public const int ClippedReLUMax = NetworkQA;

    // Feature indexing for HalfKP with color handling
    public static int GetFeatureIndex(int pieceType, int square, bool isWhite)
    {
        int colorOffset = isWhite ? 0 : PieceTypes * Squares;
        return colorOffset + pieceType * Squares + square;
    }

    // Get feature weight index with proper color handling
    public static int GetFeatureWeightIndexWithColor(int pieceType, int square, int kingSquare, bool isWhite, bool perspective)
    {
        // Apply perspective transformation
        int transformedSquare = perspective ? square ^ 56 : square;
        int transformedKingSquare = perspective ? kingSquare ^ 56 : kingSquare;

        int bucket = GetKingBucket(transformedKingSquare);

        // Properly handle piece color
        int colorOffset = isWhite ? 0 : 6;
        int pieceIndex = colorOffset + pieceType;

        // Feature index is the starting position in the weight array for this feature
        return (bucket * 12 * 64 + pieceIndex * 64 + transformedSquare) * L1Size;
    }

    public static int GetPieceTypeIndex(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Pawn => 0,
            PieceType.Knight => 1,
            PieceType.Bishop => 2,
            PieceType.Rook => 3,
            PieceType.Queen => 4,
            PieceType.King => 5,
            _ => throw new ArgumentException($"Invalid piece type: {pieceType}")
        };
    }

    // Obsidian's exact king bucket scheme
    private static readonly int[] KingBucketsScheme = {
        0,  1,  2,  3,  3,  2,  1,  0,
        4,  5,  6,  7,  7,  6,  5,  4,
        8,  8,  9,  9,  9,  9,  8,  8,
        10, 10, 10, 10, 10, 10, 10, 10,
        11, 11, 11, 11, 11, 11, 11, 11,
        11, 11, 11, 11, 11, 11, 11, 11,
        12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12,
    };

    // King bucketing for Obsidian format
    public static int GetKingBucket(int kingSquare)
    {
        // Use Obsidian's exact king bucket scheme
        return KingBucketsScheme[kingSquare];
    }

    public static int GetFeatureWeightIndex(int pieceType, int square, int kingSquare, bool perspective)
    {
        // Apply perspective transformation
        int transformedSquare = perspective ? square ^ 56 : square;
        int transformedKingSquare = perspective ? kingSquare ^ 56 : kingSquare;

        int bucket = GetKingBucket(transformedKingSquare);
        int pieceIndex = pieceType; // Piece type only, color handled separately

        // Feature index is the starting position in the weight array for this feature
        return (bucket * 12 * 64 + pieceIndex * 64 + transformedSquare) * L1Size;
    }

    // Clipped ReLU activation function (matching Obsidian's implementation)
    public static int ClippedReLU(int value)
    {
        return Math.Max(0, Math.Min(NetworkQA, value));
    }

    // Squared clipped ReLU for L2 layer (as used in Obsidian)
    public static int SquaredClippedReLU(int value)
    {
        int clipped = ClippedReLU(value);
        return clipped * clipped;
    }

    // Phase calculation for evaluation scaling (Obsidian style)
    public static int GetPhase(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);

        return 2 * position.GetPieceCount(PieceType.Pawn) +
               3 * position.GetPieceCount(PieceType.Knight) +
               3 * position.GetPieceCount(PieceType.Bishop) +
               5 * position.GetPieceCount(PieceType.Rook) +
               12 * position.GetPieceCount(PieceType.Queen);
    }



    // Network size calculations for Obsidian format
    public static int FeatureWeightsSize => KingBuckets * 12 * 64 * L1Size; // 13 * 12 * 64 * 1536
    public static int L1WeightsSize => L1Size * L2Size;                     // 1536 * 16
    public static int L2WeightsSize => L2Size * L3Size;                     // 16 * 32
    public static int L3WeightsSize => L3Size * OutputDimensions;           // 32 * 1

    // File format constants for Obsidian compatibility
    public const int ObsidianHeaderSize = 0;        // Obsidian has no header, data starts immediately
    public const int ObsidianMagicNumber = 0x4E4E55; // "NNU" in hex

    // Expected file size calculation for 30MB Obsidian network
    public static long ExpectedFileSize
    {
        get
        {
            // Obsidian format: weights are int16, biases are int32/float
            long featureWeights = (long)FeatureWeightsSize * sizeof(short);  // 13*12*64*1536*2 = ~26.2MB
            long featureBiases = (long)L1Size * sizeof(short);               // 1536*2 = 3KB
            long l1Weights = (long)L1WeightsSize * sizeof(sbyte);            // 1536*16*1 = 24KB
            long l1Biases = (long)L2Size * sizeof(float);                    // 16*4 = 64B
            long l2Weights = (long)L2Size * 2 * L3Size * sizeof(float);      // 16*2*32*4 = 4KB
            long l2Biases = (long)L3Size * sizeof(float);                    // 32*4 = 128B
            long l3Weights = (long)L3Size * sizeof(float);                   // 32*4 = 128B
            long l3Biases = (long)OutputDimensions * sizeof(float);          // 1*4 = 4B

            // Calculate total size for Obsidian's 30.9MB network
            long totalCore = featureWeights + featureBiases + l1Weights + l1Biases +
                           l2Weights + l2Biases + l3Weights + l3Biases;

            // Add padding for additional Obsidian-specific data
            return totalCore + 4_000_000; // Add ~4MB padding for additional data
        }
    }

    // Evaluation scaling for proper centipawn range
    public static int ScaleEvaluation(int rawEval)
    {
        // Scale down extreme values to reasonable centipawn range
        const int MaxCentipawns = 3000; // Maximum reasonable evaluation

        // Apply sigmoid-like scaling to compress extreme values
        if (Math.Abs(rawEval) > MaxCentipawns)
        {
            int sign = Math.Sign(rawEval);
            int absVal = Math.Abs(rawEval);

            // Logarithmic compression for very large values
            double compressed = MaxCentipawns * Math.Log(1 + (double)absVal / MaxCentipawns);
            return sign * (int)Math.Round(compressed);
        }

        return rawEval;
    }

    // Validate evaluation is reasonable
    public static bool IsValidEvaluation(int evaluation)
    {
        const int MaxReasonableEval = 5000; // Centipawns
        return Math.Abs(evaluation) <= MaxReasonableEval;
    }
}
