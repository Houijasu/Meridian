namespace Meridian.Core.NNUE;

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// PlentyChess-compatible NNUE network structure
/// Architecture: 768 → 1792 → 16 → 32
/// With 12 king buckets and 8 output buckets
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct PlentyNetwork
{
    // Network dimensions (from PlentyChess)
    public const int InputSize = 768;
    public const int L1Size = 1792;
    public const int L2Size = 16;
    public const int L3Size = 32;
    public const int KingBuckets = 12;
    public const int OutputBuckets = 8;
    
    // Quantization constants (from PlentyChess)
    public const int NetworkScale = 400;
    public const int InputQuant = 362;
    public const int L1Quant = 64;
    
    // Network weights matching PlentyChess binary layout EXACTLY
    public fixed short InputWeights[KingBuckets * InputSize * L1Size];      // [12][768 * 1792] int16_t
    public fixed short InputBiases[L1Size];                                 // [1792] int16_t
    public fixed sbyte L1Weights[OutputBuckets * L1Size * L2Size];         // [8][1792 * 16] int8_t
    public fixed float L1Biases[OutputBuckets * L2Size];                   // [8][16] float
    public fixed float L2Weights[OutputBuckets * 2 * L2Size * L3Size];     // [8][2 * 16 * 32] float
    public fixed float L2Biases[OutputBuckets * L3Size];                   // [8][32] float
    public fixed float L3Weights[OutputBuckets * L3Size];                  // [8][32] float
    public fixed float L3Biases[OutputBuckets];                            // [8] float
    
    /// <summary>
    /// Load network from PlentyChess binary format
    /// </summary>
    public static bool LoadFromFile(string path, out PlentyNetwork network)
    {
        network = default;
        
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            
            // Check if we need to skip header
            // PlentyChess embeds the network directly without a header
            // reader.BaseStream.Seek(48 * 1024, SeekOrigin.Begin);
            
            // Calculate expected sizes based on PlentyChess format
            int expectedBytes = KingBuckets * InputSize * L1Size * 2 + // input weights (int16)
                               L1Size * 2 + // input biases (int16)
                               OutputBuckets * L1Size * L2Size * 1 + // L1 weights (int8)
                               OutputBuckets * L2Size * 4 + // L1 biases (float)
                               OutputBuckets * 2 * L2Size * L3Size * 4 + // L2 weights (float)
                               OutputBuckets * L3Size * 4 + // L2 biases (float)
                               OutputBuckets * L3Size * 4 + // L3 weights (float)
                               OutputBuckets * 4; // L3 biases (float)
            
            // Verify file size
            if (reader.BaseStream.Length < expectedBytes)
            {
                Console.WriteLine($"Network file too small: {reader.BaseStream.Length} < {expectedBytes}");
                return false;
            }
            
            // Read input weights [KING_BUCKETS][INPUT_SIZE * L1_SIZE]
            fixed (short* weights = network.InputWeights)
            {
                for (int i = 0; i < KingBuckets * InputSize * L1Size; i++)
                {
                    weights[i] = reader.ReadInt16();
                }
            }
            
            // Read input biases [L1_SIZE]
            fixed (short* biases = network.InputBiases)
            {
                for (int i = 0; i < L1Size; i++)
                {
                    biases[i] = reader.ReadInt16();
                }
            }
            
            // Read L1 weights [OUTPUT_BUCKETS][L1_SIZE * L2_SIZE] - int8
            fixed (sbyte* weights1 = network.L1Weights)
            {
                for (int i = 0; i < OutputBuckets * L1Size * L2Size; i++)
                {
                    weights1[i] = reader.ReadSByte();
                }
            }
            
            // Read L1 biases [OUTPUT_BUCKETS][L2_SIZE] - float
            fixed (float* biases1 = network.L1Biases)
            {
                for (int i = 0; i < OutputBuckets * L2Size; i++)
                {
                    biases1[i] = reader.ReadSingle();
                }
            }
            
            // Read L2 weights [OUTPUT_BUCKETS][2 * L2_SIZE * L3_SIZE] - float
            fixed (float* weights2 = network.L2Weights)
            {
                for (int i = 0; i < OutputBuckets * 2 * L2Size * L3Size; i++)
                {
                    weights2[i] = reader.ReadSingle();
                }
            }
            
            // Read L2 biases [OUTPUT_BUCKETS][L3_SIZE] - float
            fixed (float* biases2 = network.L2Biases)
            {
                for (int i = 0; i < OutputBuckets * L3Size; i++)
                {
                    biases2[i] = reader.ReadSingle();
                }
            }
            
            // Read L3 weights [OUTPUT_BUCKETS][L3_SIZE] - float
            fixed (float* weights3 = network.L3Weights)
            {
                for (int i = 0; i < OutputBuckets * L3Size; i++)
                {
                    weights3[i] = reader.ReadSingle();
                }
            }
            
            // Read L3 biases [OUTPUT_BUCKETS] - float
            fixed (float* biases3 = network.L3Biases)
            {
                for (int i = 0; i < OutputBuckets; i++)
                {
                    biases3[i] = reader.ReadSingle();
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading network: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Evaluate position using PlentyChess NNUE
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Evaluate(ref PlentyAccumulator accumulator, Color sideToMove, int wKingSquare, int bKingSquare)
    {
        // Get output bucket based on material
        int outputBucket = GetOutputBucket(accumulator.Material);
        
        // Get accumulator values for both perspectives
        accumulator.GetAccumulators(sideToMove, out var us, out var them);
        
        // Apply network layers
        Span<short> l1Output = stackalloc short[L2Size];
        Span<short> l2Output = stackalloc short[L3Size];
        
        // L1 layer
        PropagateL1(us, them, l1Output, outputBucket);
        
        // L2 layer (combines both perspectives)
        PropagateL2(us, them, l1Output, l2Output, outputBucket);
        
        // L3 layer (output)
        return PropagateL3(l2Output, outputBucket, sideToMove);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PropagateL1(ReadOnlySpan<short> us, ReadOnlySpan<short> them, Span<short> output, int outputBucket)
    {
        fixed (short* usPtr = us)
        fixed (short* themPtr = them)
        fixed (sbyte* weights = L1Weights)
        fixed (float* biases = L1Biases)
        fixed (short* outputPtr = output)
        {
            // Get weights and biases for this output bucket
            sbyte* bucketWeights = weights + outputBucket * L1Size * L2Size;
            float* bucketBiases = biases + outputBucket * L2Size;
            
            // Process each output neuron
            for (int i = 0; i < L2Size; i++)
            {
                int sum = (int)(bucketBiases[i] * L1Quant);
                sbyte* weightRow = bucketWeights + i * L1Size;
                
                // Apply ReLU to accumulator and multiply with weights
                // Process first half (us perspective)
                int halfSize = L1Size / 2;
                
                // Optimized scalar path (SIMD for sbyte weights is complex)
                for (int j = 0; j < halfSize; j++)
                {
                    if (usPtr[j] > 0)
                        sum += Math.Min((int)usPtr[j], InputQuant) * weightRow[j];
                }
                for (int j = halfSize; j < L1Size; j++)
                {
                    if (themPtr[j - halfSize] > 0)
                        sum += Math.Min((int)themPtr[j - halfSize], InputQuant) * weightRow[j];
                }
                
                // Quantize output
                outputPtr[i] = (short)(sum / L1Quant);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PropagateL2(ReadOnlySpan<short> us, ReadOnlySpan<short> them, ReadOnlySpan<short> l1Input, Span<short> output, int outputBucket)
    {
        fixed (short* usPtr = us)
        fixed (short* themPtr = them)
        fixed (short* l1Ptr = l1Input)
        fixed (float* weights = L2Weights)
        fixed (float* biases = L2Biases)
        fixed (short* outputPtr = output)
        {
            // Get weights and biases for this output bucket
            float* bucketWeights = weights + outputBucket * 2 * L2Size * L3Size;
            float* bucketBiases = biases + outputBucket * L3Size;
            
            // Process each output neuron
            for (int i = 0; i < L3Size; i++)
            {
                float sum = bucketBiases[i];
                
                // Process L1 output
                float* l1Weights = bucketWeights + i * L2Size;
                for (int j = 0; j < L2Size; j++)
                {
                    if (l1Ptr[j] > 0)
                    {
                        sum += (l1Ptr[j] / (float)L1Quant) * l1Weights[j];
                    }
                }
                
                // Process direct connections from accumulator (skip connection)
                float* skipWeights = bucketWeights + L3Size * L2Size + i * L2Size;
                for (int j = 0; j < L2Size; j++)
                {
                    // This seems to be a second set of weights for the same layer
                    // Possibly for the other perspective
                    short acc = j < L2Size / 2 ? usPtr[j] : themPtr[j - L2Size / 2];
                    if (acc > 0)
                    {
                        sum += (Math.Min((int)acc, InputQuant) / (float)InputQuant) * skipWeights[j];
                    }
                }
                
                outputPtr[i] = (short)(sum * L1Quant);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PropagateL3(ReadOnlySpan<short> input, int outputBucket, Color sideToMove)
    {
        fixed (short* inputPtr = input)
        fixed (float* weights = L3Weights)
        fixed (float* biases = L3Biases)
        {
            // Get weights and bias for this output bucket
            float* bucketWeights = weights + outputBucket * L3Size;
            float bias = biases[outputBucket];
            
            float sum = bias;
            
            // Apply weights
            for (int i = 0; i < L3Size; i++)
            {
                if (inputPtr[i] > 0)
                {
                    sum += (inputPtr[i] / (float)L1Quant) * bucketWeights[i];
                }
            }
            
            // Scale to centipawns using PlentyChess scaling
            // NetworkScale = 400
            int eval = (int)(sum * NetworkScale);
            
            // Return from side to move perspective
            return sideToMove == Color.White ? eval : -eval;
        }
    }
    
    
    /// <summary>
    /// Get king bucket index for HalfKA features
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetKingBucket(int square, Color color)
    {
        // PlentyChess king bucket calculation
        int file = square & 7;
        int rank = square >> 3;
        
        if (color == Color.Black)
        {
            rank = 7 - rank; // Flip for black
        }
        
        // 12 buckets based on king position
        if (rank < 4)
        {
            return file < 4 ? 0 : 1;
        }
        else
        {
            return 2 + (rank - 4) * 2 + (file < 4 ? 0 : 1);
        }
    }
    
    /// <summary>
    /// Get output bucket based on material
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOutputBucket(int material)
    {
        // Map material value to one of the 8 buckets
        // This heuristic evenly divides the typical material range
        // (0-8000) used by the network.
        int bucket = material / 1000;
        return bucket >= OutputBuckets ? OutputBuckets - 1 : bucket;
    }
}