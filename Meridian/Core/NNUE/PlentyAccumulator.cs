namespace Meridian.Core.NNUE;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope

/// <summary>
/// Accumulator for PlentyChess NNUE with incremental updates
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 32)]
public unsafe struct PlentyAccumulator
{
    public const int MaxDepth = 128;
    
    // Accumulator state for white and black perspectives
    private fixed short _whiteAccumulator[PlentyNetwork.L1Size * MaxDepth];
    private fixed short _blackAccumulator[PlentyNetwork.L1Size * MaxDepth];
    
    // Track which ply we're at
    private int _currentPly;
    
    // Material count for output bucket selection
    public int Material { get; private set; }
    
    /// <summary>
    /// Initialize accumulator with starting position
    /// </summary>
    public void Initialize(ref PlentyNetwork network, ref BoardState board)
    {
        _currentPly = 0;
        
        // Use cached material if available, otherwise calculate
        if (board.CachedMaterial == 0)
        {
            board.CachedMaterial = board.CalculateMaterial();
        }
        Material = board.CachedMaterial;
        
        // Get king positions
        int wKingSquare = Bitboard.BitScanForward(board.WhiteKing);
        int bKingSquare = Bitboard.BitScanForward(board.BlackKing);
        
        // Extract features and initialize
        const int maxFeatures = PlentyFeatures.MaxActiveFeatures;
        Span<int> features = stackalloc int[maxFeatures * 2];
        var whiteFeatures = features[..maxFeatures];
        var blackFeatures = features[maxFeatures..];
        
        int numFeatures = PlentyFeatures.ExtractFeatures(ref board, whiteFeatures, blackFeatures);
        
        // Initialize from scratch - create slices to avoid CS9080
        var whiteSlice = whiteFeatures[..numFeatures];
        var blackSlice = blackFeatures[..numFeatures];
        RefreshAccumulator(ref network, whiteSlice, blackSlice, 0, wKingSquare, bKingSquare);
    }
    
    /// <summary>
    /// Push a new accumulator state (before making a move)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push()
    {
        if (_currentPly >= MaxDepth - 1) return;
        
        // Copy current accumulator to next ply
        fixed (short* srcWhite = &_whiteAccumulator[_currentPly * PlentyNetwork.L1Size])
        fixed (short* srcBlack = &_blackAccumulator[_currentPly * PlentyNetwork.L1Size])
        fixed (short* dstWhite = &_whiteAccumulator[(_currentPly + 1) * PlentyNetwork.L1Size])
        fixed (short* dstBlack = &_blackAccumulator[(_currentPly + 1) * PlentyNetwork.L1Size])
        {
            CopyAccumulator(srcWhite, dstWhite, PlentyNetwork.L1Size);
            CopyAccumulator(srcBlack, dstBlack, PlentyNetwork.L1Size);
        }
        
        _currentPly++;
    }
    
    /// <summary>
    /// Pop accumulator state (after unmaking a move)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Pop()
    {
        if (_currentPly > 0) _currentPly--;
    }
    
    /// <summary>
    /// Update accumulator incrementally for a move
    /// </summary>
    public void UpdateAccumulator(ref PlentyNetwork network, ref BoardState board, Move move)
    {
        Piece movingPiece = board.GetPieceType(move.From);
        Color movingColor = board.GetPieceColor(move.From);

        int wKingSquare = Bitboard.BitScanForward(board.WhiteKing);
        int bKingSquare = Bitboard.BitScanForward(board.BlackKing);

        if (movingPiece == Piece.King)
        {
            if (movingColor == Color.White)
                wKingSquare = (int)move.To;
            else
                bKingSquare = (int)move.To;

            // Recompute from scratch when king moves
            BoardState temp = board;
            temp.MakeMove(move);

            const int maxFeat = PlentyFeatures.MaxActiveFeatures;
            Span<int> buf = stackalloc int[maxFeat * 2];
            var wf = buf[..maxFeat];
            var bf = buf[maxFeat..];
            int cnt = PlentyFeatures.ExtractFeatures(ref temp, wf, bf);

            Material = temp.CachedMaterial != 0 ? temp.CachedMaterial : temp.CalculateMaterial();
            RefreshAccumulator(ref network, wf[..cnt], bf[..cnt], _currentPly, wKingSquare, bKingSquare);
            return;
        }

        Square whiteKing = (Square)wKingSquare;
        Square blackKing = (Square)bKingSquare;

        int whiteKingBucket = PlentyNetwork.GetKingBucket(wKingSquare, Color.White);
        int blackKingBucket = PlentyNetwork.GetKingBucket(bKingSquare, Color.Black);
        
        // Allocate all features in one span to avoid CS9080
        Span<int> allFeatures = stackalloc int[16]; // 4 * 4 arrays
        var removedWhite = allFeatures[..4];
        var removedBlack = allFeatures[4..8];
        var addedWhite = allFeatures[8..12];
        var addedBlack = allFeatures[12..16];
        
        PlentyFeatures.GetChangedFeatures(
            ref board, move, whiteKing, blackKing,
            removedWhite, removedBlack,
            addedWhite, addedBlack,
            out int numRemoved, out int numAdded);
        
        // Update material if capturing
        if (move.IsCapture())
        {
            // Recalculate and cache material after capture
            board.CachedMaterial = board.CalculateMaterial();
            Material = board.CachedMaterial;
        }
        
        // Update both perspectives
        fixed (short* whiteAcc = &_whiteAccumulator[_currentPly * PlentyNetwork.L1Size])
        fixed (short* blackAcc = &_blackAccumulator[_currentPly * PlentyNetwork.L1Size])
        {
            // Remove old features
            for (int i = 0; i < numRemoved; i++)
            {
                SubtractFeature(ref network, removedWhite[i], whiteAcc, whiteKingBucket);
                SubtractFeature(ref network, removedBlack[i], blackAcc, blackKingBucket);
            }
            
            // Add new features
            for (int i = 0; i < numAdded; i++)
            {
                AddFeature(ref network, addedWhite[i], whiteAcc, whiteKingBucket);
                AddFeature(ref network, addedBlack[i], blackAcc, blackKingBucket);
            }
        }
    }
    
    /// <summary>
    /// Get current accumulator values for evaluation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetAccumulators(Color sideToMove, out ReadOnlySpan<short> us, out ReadOnlySpan<short> them)
    {
        fixed (short* white = &_whiteAccumulator[_currentPly * PlentyNetwork.L1Size])
        fixed (short* black = &_blackAccumulator[_currentPly * PlentyNetwork.L1Size])
        {
            if (sideToMove == Color.White)
            {
                us = new ReadOnlySpan<short>(white, PlentyNetwork.L1Size);
                them = new ReadOnlySpan<short>(black, PlentyNetwork.L1Size);
            }
            else
            {
                us = new ReadOnlySpan<short>(black, PlentyNetwork.L1Size);
                them = new ReadOnlySpan<short>(white, PlentyNetwork.L1Size);
            }
        }
    }
    
    /// <summary>
    /// Refresh accumulator from scratch
    /// </summary>
    private void RefreshAccumulator(ref PlentyNetwork network, ReadOnlySpan<int> whiteFeatures, ReadOnlySpan<int> blackFeatures, int ply, int wKingSquare, int bKingSquare)
    {
        // Get king buckets
        int whiteKingBucket = PlentyNetwork.GetKingBucket(wKingSquare, Color.White);
        int blackKingBucket = PlentyNetwork.GetKingBucket(bKingSquare, Color.Black);
        
        fixed (short* whiteAcc = &_whiteAccumulator[ply * PlentyNetwork.L1Size])
        fixed (short* blackAcc = &_blackAccumulator[ply * PlentyNetwork.L1Size])
        {
            // Start with biases
            CopyBiases(ref network, whiteAcc);
            CopyBiases(ref network, blackAcc);
            
            // Add active features with king buckets
            foreach (int feature in whiteFeatures)
            {
                AddFeature(ref network, feature, whiteAcc, whiteKingBucket);
            }
            
            foreach (int feature in blackFeatures)
            {
                AddFeature(ref network, feature, blackAcc, blackKingBucket);
            }
            
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyAccumulator(short* src, short* dst, int size)
    {
        if (Avx2.IsSupported)
        {
            int vectorSize = Vector256<short>.Count;
            int i = 0;
            
            for (; i + vectorSize <= size; i += vectorSize)
            {
                var vec = Avx.LoadVector256(src + i);
                Avx.Store(dst + i, vec);
            }
            
            // Handle remaining elements
            for (; i < size; i++)
            {
                dst[i] = src[i];
            }
        }
        else
        {
            new Span<short>(src, size).CopyTo(new Span<short>(dst, size));
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyBiases(ref PlentyNetwork network, short* accumulator)
    {
        fixed (short* biases = network.InputBiases)
        {
            CopyAccumulator(biases, accumulator, PlentyNetwork.L1Size);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddFeature(ref PlentyNetwork network, int featureIndex, short* accumulator, int kingBucket)
    {
        fixed (short* weights = network.InputWeights)
        {
            // Get weights for the specific king bucket
            short* bucketWeights = weights + kingBucket * PlentyNetwork.InputSize * PlentyNetwork.L1Size;
            short* featureWeights = bucketWeights + featureIndex * PlentyNetwork.L1Size;
            
            if (Avx2.IsSupported)
            {
                int vectorSize = Vector256<short>.Count;
                int i = 0;
                
                for (; i + vectorSize <= PlentyNetwork.L1Size; i += vectorSize)
                {
                    var acc = Avx.LoadVector256(accumulator + i);
                    var weight = Avx.LoadVector256(featureWeights + i);
                    var result = Avx2.Add(acc, weight);
                    Avx.Store(accumulator + i, result);
                }
                
                // Handle remaining
                for (; i < PlentyNetwork.L1Size; i++)
                {
                    accumulator[i] = (short)(accumulator[i] + featureWeights[i]);
                }
            }
            else
            {
                for (int i = 0; i < PlentyNetwork.L1Size; i++)
                {
                    accumulator[i] = (short)(accumulator[i] + featureWeights[i]);
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SubtractFeature(ref PlentyNetwork network, int featureIndex, short* accumulator, int kingBucket)
    {
        fixed (short* weights = network.InputWeights)
        {
            // Get weights for the specific king bucket
            short* bucketWeights = weights + kingBucket * PlentyNetwork.InputSize * PlentyNetwork.L1Size;
            short* featureWeights = bucketWeights + featureIndex * PlentyNetwork.L1Size;
            
            if (Avx2.IsSupported)
            {
                int vectorSize = Vector256<short>.Count;
                int i = 0;
                
                for (; i + vectorSize <= PlentyNetwork.L1Size; i += vectorSize)
                {
                    var acc = Avx.LoadVector256(accumulator + i);
                    var weight = Avx.LoadVector256(featureWeights + i);
                    var result = Avx2.Subtract(acc, weight);
                    Avx.Store(accumulator + i, result);
                }
                
                // Handle remaining
                for (; i < PlentyNetwork.L1Size; i++)
                {
                    accumulator[i] = (short)(accumulator[i] - featureWeights[i]);
                }
            }
            else
            {
                for (int i = 0; i < PlentyNetwork.L1Size; i++)
                {
                    accumulator[i] = (short)(accumulator[i] - featureWeights[i]);
                }
            }
        }
    }
}

#pragma warning restore CS9080