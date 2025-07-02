using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Meridian.Core.Board;

namespace Meridian.Core.NNUE;

public class NNUENetwork
{
    private short[] _featureWeights;
    private short[] _featureBias;
    private sbyte[] _l1Weights;
    private int[] _l1Bias;
    private sbyte[] _l2Weights;
    private int[] _l2Bias;
    private sbyte[] _l3Weights;
    private int[] _l3Bias;
    
    private readonly Accumulator[] _accumulators;
    private int _currentAccumulator;
    
    public bool IsLoaded { get; private set; }
    
    public NNUENetwork()
    {
        // Obsidian format: [KingBuckets][2][6][64][L1]
        _featureWeights = new short[NNUEConstants.KingBuckets * 2 * 6 * 64 * NNUEConstants.L1Size];
        _featureBias = new short[NNUEConstants.L1Size];
        _l1Weights = new sbyte[NNUEConstants.OutputBuckets * NNUEConstants.L1Size * NNUEConstants.L2Size];
        _l1Bias = new int[NNUEConstants.OutputBuckets * NNUEConstants.L2Size];
        _l2Weights = new sbyte[NNUEConstants.OutputBuckets * NNUEConstants.L2Size * 2 * NNUEConstants.L3Size];
        _l2Bias = new int[NNUEConstants.OutputBuckets * NNUEConstants.L3Size];
        _l3Weights = new sbyte[NNUEConstants.OutputBuckets * NNUEConstants.L3Size];
        _l3Bias = new int[NNUEConstants.OutputBuckets];
        
        _accumulators = new Accumulator[256];
        for (int i = 0; i < _accumulators.Length; i++)
        {
            _accumulators[i] = new Accumulator();
        }
        _currentAccumulator = 0;
    }
    
    public bool LoadNetwork(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            
            // Note: The Obsidian format may not match our expanded L1 weights size
            // We'll read what's available and handle mismatches
            
            for (int i = 0; i < _featureWeights.Length; i++)
            {
                _featureWeights[i] = reader.ReadInt16();
            }
            
            for (int i = 0; i < _featureBias.Length; i++)
            {
                _featureBias[i] = reader.ReadInt16();
            }
            
            for (int i = 0; i < _l1Weights.Length; i++)
            {
                _l1Weights[i] = reader.ReadSByte();
            }
            
            // L1 bias is float in Obsidian
            for (int i = 0; i < _l1Bias.Length; i++)
            {
                _l1Bias[i] = (int)(reader.ReadSingle() * NNUEConstants.QB);
            }
            
            // L2 weights are float in Obsidian
            for (int i = 0; i < _l2Weights.Length; i++)
            {
                _l2Weights[i] = (sbyte)(reader.ReadSingle() * NNUEConstants.QA);
            }
            
            // L2 bias is float in Obsidian  
            for (int i = 0; i < _l2Bias.Length; i++)
            {
                _l2Bias[i] = (int)(reader.ReadSingle() * NNUEConstants.QB);
            }
            
            // L3 weights are float in Obsidian
            for (int i = 0; i < _l3Weights.Length; i++)
            {
                _l3Weights[i] = (sbyte)(reader.ReadSingle() * NNUEConstants.QA);
            }
            
            // L3 bias is float in Obsidian
            for (int i = 0; i < _l3Bias.Length; i++)
            {
                _l3Bias[i] = (int)(reader.ReadSingle() * NNUEConstants.QB);
            }
            
            IsLoaded = true;
            return true;
        }
        catch (IOException)
        {
            IsLoaded = false;
            return false;
        }
    }
    
    public void InitializeAccumulator(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        
        if (!IsLoaded)
            return;
            
        var acc = _accumulators[_currentAccumulator];
        acc.Reset();
        
        Array.Copy(_featureBias, acc.GetAccumulation(0), NNUEConstants.L1Size);
        Array.Copy(_featureBias, acc.GetAccumulation(1), NNUEConstants.L1Size);
        
        RefreshAccumulator(position, acc);
    }
    
    public void UpdateAccumulator(Position position, Move move)
    {
        ArgumentNullException.ThrowIfNull(position);
        
        if (!IsLoaded)
            return;
            
        _currentAccumulator = (_currentAccumulator + 1) % _accumulators.Length;
        var newAcc = _accumulators[_currentAccumulator];
        var oldAcc = _accumulators[(_currentAccumulator - 1 + _accumulators.Length) % _accumulators.Length];
        
        newAcc.CopyFrom(oldAcc);
        
        int from = (int)move.From;
        int to = (int)move.To;
        var movingPiece = position.GetPieceAt(from);
        var capturedPiece = position.GetPieceAt(to);
        
        if (!movingPiece.HasValue)
            return; // Invalid move
            
        int movingColor = movingPiece.Value.GetColor() == Color.White ? 0 : 1;
        int pieceType = GetPieceTypeIndex(movingPiece.Value.Type());
        
        // Check if this is a king move
        if (pieceType == 5) // King
        {
            // King moved - refresh the entire accumulator for that perspective
            newAcc.Reset();
            Array.Copy(_featureBias, newAcc.GetAccumulation(0), NNUEConstants.L1Size);
            Array.Copy(_featureBias, newAcc.GetAccumulation(1), NNUEConstants.L1Size);
            RefreshAccumulator(position, newAcc);
            return;
        }
        
        int wKingSquare = position.GetKingSquare(true);
        int bKingSquare = position.GetKingSquare(false);
        
        // Remove moving piece from old position
        newAcc.RemovePiece(0, pieceType + movingColor * 6, from, wKingSquare, _featureWeights);
        newAcc.RemovePiece(1, pieceType + movingColor * 6, from, bKingSquare, _featureWeights);
        
        // Add moving piece to new position
        newAcc.AddPiece(0, pieceType + movingColor * 6, to, wKingSquare, _featureWeights);
        newAcc.AddPiece(1, pieceType + movingColor * 6, to, bKingSquare, _featureWeights);
        
        // Handle captures
        if (capturedPiece.HasValue)
        {
            int capturedColor = capturedPiece.Value.GetColor() == Color.White ? 0 : 1;
            int capturedType = GetPieceTypeIndex(capturedPiece.Value.Type());
            
            newAcc.RemovePiece(0, capturedType + capturedColor * 6, to, wKingSquare, _featureWeights);
            newAcc.RemovePiece(1, capturedType + capturedColor * 6, to, bKingSquare, _featureWeights);
        }
        
        // Handle castling (rook movement)
        if (move.IsCastling)
        {
            int rookFrom, rookTo;
            if (to == 6 || to == 62) // King-side castling
            {
                rookFrom = to + 1;
                rookTo = to - 1;
            }
            else // Queen-side castling
            {
                rookFrom = to - 2;
                rookTo = to + 1;
            }
            
            // Remove and add rook
            newAcc.RemovePiece(0, 3 + movingColor * 6, rookFrom, wKingSquare, _featureWeights);
            newAcc.RemovePiece(1, 3 + movingColor * 6, rookFrom, bKingSquare, _featureWeights);
            newAcc.AddPiece(0, 3 + movingColor * 6, rookTo, wKingSquare, _featureWeights);
            newAcc.AddPiece(1, 3 + movingColor * 6, rookTo, bKingSquare, _featureWeights);
        }
    }
    
    public int Evaluate(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        
        if (!IsLoaded)
            return 0;
            
        try
        {
            
        var acc = _accumulators[_currentAccumulator];
        int bucket = GetOutputBucket(position);
        
        // Transform accumulator to L1 output with clipped ReLU
        var l1Output = new sbyte[NNUEConstants.L1Size * 2];
        TransformAccumulator(acc, position.SideToMove == Color.White, l1Output);
        
        // Apply squared activation and propagate to L2
        var l1Squared = new float[NNUEConstants.L2Size * 2];
        var l2Output = new float[NNUEConstants.L2Size * 2];
        
        // Initialize with bias for both perspectives
        for (int i = 0; i < NNUEConstants.L2Size; i++)
        {
            l2Output[i] = _l1Bias[bucket * NNUEConstants.L2Size + i] / (float)NNUEConstants.QB;
            l2Output[i + NNUEConstants.L2Size] = l2Output[i];
        }
        
        // Propagate L1 to L2 with squared activation
        PropagateL1Squared(l1Output, l2Output, bucket);
        
        // Apply ReLU to L2 output
        var l2Input = new sbyte[NNUEConstants.L2Size * 2];
        for (int i = 0; i < NNUEConstants.L2Size * 2; i++)
        {
            l2Input[i] = (sbyte)Math.Max(0, Math.Min(127, (int)(l2Output[i] * NNUEConstants.QB)));
        }
        
        // Initialize L3 output with bias
        var l3Output = new int[NNUEConstants.L3Size];
        Array.Copy(_l2Bias, bucket * NNUEConstants.L3Size, l3Output, 0, NNUEConstants.L3Size);
        
        PropagateL2(l2Input, l3Output, bucket);
        
        var l3Input = new sbyte[NNUEConstants.L3Size];
        ClampedReLU(l3Output, l3Input);
        
        // The output needs to be scaled properly
        // Obsidian typically uses a different scaling factor
        int rawOutput = PropagateL3(l3Input, bucket);
        return rawOutput * NNUEConstants.NetworkScale / (NNUEConstants.QA * NNUEConstants.QB);
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Evaluate error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return 0;
        }
    }
    
    private void RefreshAccumulator(Position position, Accumulator acc)
    {
        int wKingSquare = position.GetKingSquare(true);
        int bKingSquare = position.GetKingSquare(false);
        
        for (int sq = 0; sq < 64; sq++)
        {
            var piece = position.GetPieceAt(sq);
            if (piece.HasValue)
            {
                int color = piece.Value.GetColor() == Color.White ? 0 : 1;
                int pieceType = GetPieceTypeIndex(piece.Value.Type());
                
                // White perspective
                acc.AddPiece(0, pieceType + color * 6, sq, wKingSquare, _featureWeights);
                // Black perspective - flip the square but not the king square for bucket calculation
                acc.AddPiece(1, pieceType + color * 6, sq, bKingSquare, _featureWeights);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceTypeIndex(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => 0,
            PieceType.Knight => 1,
            PieceType.Bishop => 2,
            PieceType.Rook => 3,
            PieceType.Queen => 4,
            PieceType.King => 5,
            _ => 0
        };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOutputBucket(Position position)
    {
        int pieceCount = position.GetPieceCount();
        return Math.Min((int)pieceCount / 4, NNUEConstants.OutputBuckets - 1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void TransformAccumulator(Accumulator acc, bool whiteToMove, sbyte[] output)
    {
        var us = acc.GetAccumulation(whiteToMove ? 0 : 1);
        var them = acc.GetAccumulation(whiteToMove ? 1 : 0);
        
        fixed (short* usPtr = us)
        fixed (short* themPtr = them)
        fixed (sbyte* outPtr = output)
        {
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                outPtr[i] = ClampToSByte(usPtr[i]);
                outPtr[i + NNUEConstants.L1Size] = ClampToSByte(themPtr[i]);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte ClampToSByte(short value)
    {
        return (sbyte)Math.Max(0, Math.Min(127, (int)value));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PropagateL1Squared(sbyte[] input, float[] output, int bucket)
    {
        int weightOffset = bucket * NNUEConstants.L1Size * NNUEConstants.L2Size;
        
        fixed (sbyte* inPtr = input)
        fixed (sbyte* wPtr = &_l1Weights[weightOffset])
        fixed (float* outPtr = output)
        {
            // The weights are arranged for a single perspective
            // We need to apply them to both perspectives in the output
            for (int i = 0; i < NNUEConstants.L2Size; i++)
            {
                // Process white perspective (first half of input)
                float sum0 = 0;
                for (int j = 0; j < NNUEConstants.L1Size; j++)
                {
                    if (inPtr[j] > 0) // Only non-zero values
                    {
                        float val = inPtr[j] / (float)NNUEConstants.QA;
                        sum0 += val * val * wPtr[i * NNUEConstants.L1Size + j]; // Squared activation
                    }
                }
                outPtr[i] += sum0 / NNUEConstants.QA;
                
                // Process black perspective (second half of input)
                float sum1 = 0;
                for (int j = 0; j < NNUEConstants.L1Size; j++)
                {
                    if (inPtr[NNUEConstants.L1Size + j] > 0) // Only non-zero values
                    {
                        float val = inPtr[NNUEConstants.L1Size + j] / (float)NNUEConstants.QA;
                        sum1 += val * val * wPtr[i * NNUEConstants.L1Size + j]; // Squared activation
                    }
                }
                outPtr[NNUEConstants.L2Size + i] += sum1 / NNUEConstants.QA;
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void PropagateL2(sbyte[] input, int[] output, int bucket)
    {
        int weightOffset = bucket * NNUEConstants.L2Size * 2 * NNUEConstants.L3Size;
        
        fixed (sbyte* inPtr = input)
        fixed (sbyte* wPtr = &_l2Weights[weightOffset])
        fixed (int* outPtr = output)
        {
            for (int i = 0; i < NNUEConstants.L3Size; i++)
            {
                int sum = 0;
                for (int j = 0; j < NNUEConstants.L2Size * 2; j++)
                {
                    sum += inPtr[j] * wPtr[i * NNUEConstants.L2Size * 2 + j];
                }
                outPtr[i] += sum;
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int PropagateL3(sbyte[] input, int bucket)
    {
        int weightOffset = bucket * NNUEConstants.L3Size;
        int sum = _l3Bias[bucket];
        
        fixed (sbyte* inPtr = input)
        fixed (sbyte* wPtr = &_l3Weights[weightOffset])
        {
            for (int i = 0; i < NNUEConstants.L3Size; i++)
            {
                sum += inPtr[i] * wPtr[i];
            }
        }
        
        return sum / NNUEConstants.QB;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClampedReLU(int[] input, sbyte[] output)
    {
        for (int i = 0; i < output.Length; i++)
        {
            output[i] = (sbyte)Math.Max(0, Math.Min(127, input[i] / NNUEConstants.QB));
        }
    }
}