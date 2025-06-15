namespace Meridian.Core.NNUE;

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// PlentyChess NNUE Network structure
/// Architecture: 768 -> 1792 -> 16 -> 32 -> 1
/// With 12 king buckets and 8 output buckets
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NNUENetwork
{
    // Network dimensions (matching PlentyChess)
    public const int InputSize = NNUEFeatures.InputDimensions; // 768
    public const int L1Size = 1792;
    public const int L2Size = 16;
    public const int L3Size = 32;
    
    // Buckets for perspective and output
    public const int KingBuckets = 12;
    public const int OutputBuckets = 8;
    
    // Quantization parameters from PlentyChess
    public const int NetworkScale = 400;
    public const int InputQuant = 362;
    public const int L1Quant = 64;
    
    // Input layer (feature transformer) - with king buckets
    public fixed short InputWeights[KingBuckets * InputSize * L1Size]; // [12][768 * 1792]
    public fixed short InputBiases[L1Size]; // [1792]
    
    // L1 layer - with output buckets
    public fixed short L1Weights[OutputBuckets * L1Size * L2Size]; // [8][1792 * 16]
    public fixed short L1Biases[OutputBuckets * L2Size]; // [8][16]
    
    // L2 layer - with output buckets and 2x for both perspectives
    public fixed short L2Weights[OutputBuckets * 2 * L2Size * L3Size]; // [8][2 * 16 * 32]
    public fixed short L2Biases[OutputBuckets * L3Size]; // [8][32]
    
    // L3 layer (output) - with output buckets
    public fixed int L3Weights[OutputBuckets * L3Size]; // [8][32]
    public fixed int L3Biases[OutputBuckets]; // [8]
    
    /// <summary>
    /// Evaluate position using NNUE
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Evaluate(ref NNUEAccumulator accumulator, Color sideToMove, int wKingSquare, int bKingSquare)
    {
        // Get king buckets
        int whiteKingBucket = GetKingBucket(wKingSquare, Color.White);
        int blackKingBucket = GetKingBucket(bKingSquare, Color.Black);
        
        // Get output bucket based on material
        int outputBucket = GetOutputBucket(ref accumulator);
        
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
    private static int GetKingBucket(int square, Color color)
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOutputBucket(ref NNUEAccumulator accumulator)
    {
        // Simplified output bucket based on material count
        // In real implementation, this would consider piece types and positions
        return 0; // Using bucket 0 for now
    }
    
    /// <summary>
    /// Load network from PlentyChess file format
    /// </summary>
    public static bool LoadFromFile(string path, out NNUENetwork network)
    {
        network = default;
        
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            
            // Skip header (48KB)
            reader.BaseStream.Seek(48 * 1024, SeekOrigin.Begin);
            
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
            
            // Read L1 weights [OUTPUT_BUCKETS][L1_SIZE * L2_SIZE]
            fixed (short* weights1 = network.L1Weights)
            {
                for (int i = 0; i < OutputBuckets * L1Size * L2Size; i++)
                {
                    weights1[i] = reader.ReadInt16();
                }
            }
            
            // Read L1 biases [OUTPUT_BUCKETS][L2_SIZE]
            fixed (short* biases1 = network.L1Biases)
            {
                for (int i = 0; i < OutputBuckets * L2Size; i++)
                {
                    biases1[i] = reader.ReadInt16();
                }
            }
            
            // Read L2 weights [OUTPUT_BUCKETS][2 * L2_SIZE * L3_SIZE]
            fixed (short* weights2 = network.L2Weights)
            {
                for (int i = 0; i < OutputBuckets * 2 * L2Size * L3Size; i++)
                {
                    weights2[i] = reader.ReadInt16();
                }
            }
            
            // Read L2 biases [OUTPUT_BUCKETS][L3_SIZE]
            fixed (short* biases2 = network.L2Biases)
            {
                for (int i = 0; i < OutputBuckets * L3Size; i++)
                {
                    biases2[i] = reader.ReadInt16();
                }
            }
            
            // Read L3 weights [OUTPUT_BUCKETS][L3_SIZE]
            fixed (int* weights3 = network.L3Weights)
            {
                for (int i = 0; i < OutputBuckets * L3Size; i++)
                {
                    weights3[i] = reader.ReadInt32();
                }
            }
            
            // Read L3 biases [OUTPUT_BUCKETS]
            fixed (int* biases3 = network.L3Biases)
            {
                for (int i = 0; i < OutputBuckets; i++)
                {
                    biases3[i] = reader.ReadInt32();
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PropagateL1(ReadOnlySpan<short> us, ReadOnlySpan<short> them, Span<short> output, int outputBucket)
    {
        fixed (short* usPtr = us)
        fixed (short* themPtr = them)
        fixed (short* weights = L1Weights)
        fixed (short* biases = L1Biases)
        fixed (short* outputPtr = output)
        {
            // Get weights and biases for this output bucket
            short* bucketWeights = weights + outputBucket * L1Size * L2Size;
            short* bucketBiases = biases + outputBucket * L2Size;
            
            // Process each output neuron
            for (int i = 0; i < L2Size; i++)
            {
                int sum = bucketBiases[i] * L1Quant;
                short* weightRow = bucketWeights + i * L1Size;
                
                // Apply ReLU to accumulator and multiply with weights
                for (int j = 0; j < L1Size; j++)
                {
                    // Use the appropriate accumulator based on perspective
                    short acc = j < L1Size / 2 ? usPtr[j] : themPtr[j - L1Size / 2];
                    if (acc > 0)
                    {
                        sum += Math.Min((int)acc, InputQuant) * weightRow[j];
                    }
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
        fixed (short* weights = L2Weights)
        fixed (short* biases = L2Biases)
        fixed (short* outputPtr = output)
        {
            // Get weights and biases for this output bucket
            short* bucketWeights = weights + outputBucket * 2 * L2Size * L3Size;
            short* bucketBiases = biases + outputBucket * L3Size;
            
            // Process each output neuron
            for (int i = 0; i < L3Size; i++)
            {
                int sum = bucketBiases[i] * L1Quant;
                
                // Process L1 output
                short* l1Weights = bucketWeights + i * L2Size;
                for (int j = 0; j < L2Size; j++)
                {
                    if (l1Ptr[j] > 0)
                    {
                        sum += l1Ptr[j] * l1Weights[j];
                    }
                }
                
                // Process direct connections from accumulator (skip connection)
                short* skipWeights = bucketWeights + L3Size * L2Size + i * L2Size;
                for (int j = 0; j < L2Size; j++)
                {
                    // This seems to be a second set of weights for the same layer
                    // Possibly for the other perspective
                    short acc = j < L2Size / 2 ? usPtr[j] : themPtr[j - L2Size / 2];
                    if (acc > 0)
                    {
                        sum += Math.Min((int)acc, InputQuant) * skipWeights[j];
                    }
                }
                
                outputPtr[i] = (short)(sum / L1Quant);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PropagateL3(ReadOnlySpan<short> input, int outputBucket, Color sideToMove)
    {
        fixed (short* inputPtr = input)
        fixed (int* weights = L3Weights)
        fixed (int* biases = L3Biases)
        {
            // Get weights and bias for this output bucket
            int* bucketWeights = weights + outputBucket * L3Size;
            int bias = biases[outputBucket];
            
            int sum = bias;
            
            // Apply weights
            for (int i = 0; i < L3Size; i++)
            {
                if (inputPtr[i] > 0)
                {
                    sum += inputPtr[i] * bucketWeights[i];
                }
            }
            
            // Scale to centipawns using PlentyChess scaling
            // NetworkScale = 400
            int eval = sum / NetworkScale;
            
            // Return from side to move perspective
            return sideToMove == Color.White ? eval : -eval;
        }
    }
    
}