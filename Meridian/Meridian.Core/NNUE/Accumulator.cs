using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Meridian.Core.NNUE;

#pragma warning disable CA1051, CA1815
public struct DirtyPiece
{
    public int Piece;
    public int From;
    public int To;
}
#pragma warning restore CA1051, CA1815

public class Accumulator
{
    private readonly short[][] _accumulation;
    private readonly bool[] _computed;

    public Accumulator()
    {
        _accumulation = new short[2][];
        for (int perspective = 0; perspective < 2; perspective++)
        {
            _accumulation[perspective] = new short[NNUEConstants.L1Size];
        }
        _computed = new bool[2];
    }

    public void Reset()
    {
        _computed[0] = false;
        _computed[1] = false;

        // Clear accumulation arrays
        Array.Clear(_accumulation[0]);
        Array.Clear(_accumulation[1]);
    }

    public void AddFeature(int perspective, int featureIndex, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (featureIndex < 0 || featureIndex + NNUEConstants.L1Size > weights.Length)
        {
            Console.WriteLine($"NNUE Accumulator: Feature index {featureIndex} out of bounds for weights array of length {weights.Length}");
            return; // Gracefully handle instead of throwing
        }

        if (perspective < 0 || perspective >= 2)
        {
            Console.WriteLine($"NNUE Accumulator: Invalid perspective {perspective}, must be 0 or 1");
            return; // Gracefully handle instead of throwing
        }

        try
        {
            unsafe
            {
                fixed (short* acc = &_accumulation[perspective][0])
                fixed (short* w = &weights[featureIndex])
                {
                    AddFeatureVector(acc, w);
                }
            }
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Access violation in AddFeature: {ex.Message}");
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Index out of range in AddFeature: {ex.Message}");
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Null reference in AddFeature: {ex.Message}");
        }
    }

    public void SubtractFeature(int perspective, int featureIndex, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (featureIndex < 0 || featureIndex + NNUEConstants.L1Size > weights.Length)
        {
            Console.WriteLine($"NNUE Accumulator: Feature index {featureIndex} out of bounds for weights array of length {weights.Length}");
            return; // Gracefully handle instead of throwing
        }

        if (perspective < 0 || perspective >= 2)
        {
            Console.WriteLine($"NNUE Accumulator: Invalid perspective {perspective}, must be 0 or 1");
            return; // Gracefully handle instead of throwing
        }

        try
        {
            unsafe
            {
                fixed (short* acc = &_accumulation[perspective][0])
                fixed (short* w = &weights[featureIndex])
                {
                    SubtractFeatureVector(acc, w);
                }
            }
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Access violation in SubtractFeature: {ex.Message}");
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Index out of range in SubtractFeature: {ex.Message}");
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Null reference in SubtractFeature: {ex.Message}");
        }
    }

    public void AddPiece(int perspective, int pieceType, int square, int kingSquare, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        int featureIndex = NNUEConstants.GetFeatureWeightIndex(pieceType, square, kingSquare, perspective == 1);
        AddFeature(perspective, featureIndex, weights);
    }

    public void RemovePiece(int perspective, int pieceType, int square, int kingSquare, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        int featureIndex = NNUEConstants.GetFeatureWeightIndex(pieceType, square, kingSquare, perspective == 1);
        SubtractFeature(perspective, featureIndex, weights);
    }

    public void MovePiece(int perspective, int pieceType, int from, int to, int kingSquare, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        int fromFeatureIndex = NNUEConstants.GetFeatureWeightIndex(pieceType, from, kingSquare, perspective == 1);
        int toFeatureIndex = NNUEConstants.GetFeatureWeightIndex(pieceType, to, kingSquare, perspective == 1);

        if (fromFeatureIndex < 0 || fromFeatureIndex + NNUEConstants.L1Size > weights.Length ||
            toFeatureIndex < 0 || toFeatureIndex + NNUEConstants.L1Size > weights.Length)
        {
            Console.WriteLine($"NNUE Accumulator: Feature indices out of bounds. From: {fromFeatureIndex}, To: {toFeatureIndex}, Array length: {weights.Length}");
            return; // Gracefully handle instead of throwing
        }

        try
        {
            unsafe
            {
                fixed (short* acc = &_accumulation[perspective][0])
                fixed (short* wFrom = &weights[fromFeatureIndex])
                fixed (short* wTo = &weights[toFeatureIndex])
                {
                    SubtractFeatureVector(acc, wFrom);
                    AddFeatureVector(acc, wTo);
                }
            }
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Access violation in MovePiece: {ex.Message}");
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Index out of range in MovePiece: {ex.Message}");
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Null reference in MovePiece: {ex.Message}");
        }
    }

    public short[] GetAccumulation(int perspective)
    {
        if (perspective < 0 || perspective >= 2)
        {
            throw new ArgumentOutOfRangeException(nameof(perspective), "Perspective must be 0 or 1");
        }

        return _accumulation[perspective];
    }

    public void CopyFrom(Accumulator other)
    {
        ArgumentNullException.ThrowIfNull(other);

        for (int perspective = 0; perspective < 2; perspective++)
        {
            _computed[perspective] = other._computed[perspective];
            Array.Copy(other._accumulation[perspective], _accumulation[perspective], NNUEConstants.L1Size);
        }
    }

    public bool IsComputed(int perspective)
    {
        return _computed[perspective];
    }

    public void SetComputed(int perspective, bool value)
    {
        _computed[perspective] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void AddFeatureVector(short* acc, short* weights)
    {
        try
        {
            // Use SIMD instructions if available for better performance
            if (Avx2.IsSupported)
            {
                int chunks = NNUEConstants.L1Size / 16;
                for (int i = 0; i < chunks; i++)
                {
                    var accVec = Avx.LoadVector256(acc + i * 16);
                    var weightVec = Avx.LoadVector256(weights + i * 16);
                    var result = Avx2.Add(accVec, weightVec);
                    Avx.Store(acc + i * 16, result);
                }

                // Handle remaining elements
                for (int i = chunks * 16; i < NNUEConstants.L1Size; i++)
                {
                    acc[i] += weights[i];
                }
            }
            else if (Sse2.IsSupported)
            {
                int chunks = NNUEConstants.L1Size / 8;
                for (int i = 0; i < chunks; i++)
                {
                    var accVec = Sse2.LoadVector128(acc + i * 8);
                    var weightVec = Sse2.LoadVector128(weights + i * 8);
                    var result = Sse2.Add(accVec, weightVec);
                    Sse2.Store(acc + i * 8, result);
                }

                // Handle remaining elements
                for (int i = chunks * 8; i < NNUEConstants.L1Size; i++)
                {
                    acc[i] += weights[i];
                }
            }
            else
            {
                // Fallback to scalar operations
                for (int i = 0; i < NNUEConstants.L1Size; i++)
                {
                    acc[i] += weights[i];
                }
            }
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Access violation in AddFeatureVector: {ex.Message}");
            // Fallback to safe scalar operations
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                acc[i] += weights[i];
            }
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Index out of range in AddFeatureVector: {ex.Message}");
            // Fallback to safe scalar operations
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                acc[i] += weights[i];
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Invalid operation in AddFeatureVector: {ex.Message}");
            // Fallback to safe scalar operations
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                acc[i] += weights[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void SubtractFeatureVector(short* acc, short* weights)
    {
        try
        {
            // Use SIMD instructions if available for better performance
            if (Avx2.IsSupported)
            {
                int chunks = NNUEConstants.L1Size / 16;
                for (int i = 0; i < chunks; i++)
                {
                    var accVec = Avx.LoadVector256(acc + i * 16);
                    var weightVec = Avx.LoadVector256(weights + i * 16);
                    var result = Avx2.Subtract(accVec, weightVec);
                    Avx.Store(acc + i * 16, result);
                }

                // Handle remaining elements
                for (int i = chunks * 16; i < NNUEConstants.L1Size; i++)
                {
                    acc[i] -= weights[i];
                }
            }
            else if (Sse2.IsSupported)
            {
                int chunks = NNUEConstants.L1Size / 8;
                for (int i = 0; i < chunks; i++)
                {
                    var accVec = Sse2.LoadVector128(acc + i * 8);
                    var weightVec = Sse2.LoadVector128(weights + i * 8);
                    var result = Sse2.Subtract(accVec, weightVec);
                    Sse2.Store(acc + i * 8, result);
                }

                // Handle remaining elements
                for (int i = chunks * 8; i < NNUEConstants.L1Size; i++)
                {
                    acc[i] -= weights[i];
                }
            }
            else
            {
                // Fallback to scalar operations
                for (int i = 0; i < NNUEConstants.L1Size; i++)
                {
                    acc[i] -= weights[i];
                }
            }
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Access violation in SubtractFeatureVector: {ex.Message}");
            // Fallback to safe scalar operations
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                acc[i] -= weights[i];
            }
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Index out of range in SubtractFeatureVector: {ex.Message}");
            // Fallback to safe scalar operations
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                acc[i] -= weights[i];
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"NNUE Accumulator: Invalid operation in SubtractFeatureVector: {ex.Message}");
            // Fallback to safe scalar operations
            for (int i = 0; i < NNUEConstants.L1Size; i++)
            {
                acc[i] -= weights[i];
            }
        }
    }

    // Diagnostic methods for debugging
    public void PrintAccumulation(int perspective, int maxElements = 10)
    {
        Console.WriteLine($"Accumulation[{perspective}] (first {maxElements} elements):");
        for (int i = 0; i < Math.Min(maxElements, NNUEConstants.L1Size); i++)
        {
            Console.Write($"{_accumulation[perspective][i]:+0000;-0000} ");
        }
        Console.WriteLine();
    }

    public int GetAccumulationSum(int perspective)
    {
        int sum = 0;
        for (int i = 0; i < NNUEConstants.L1Size; i++)
        {
            sum += _accumulation[perspective][i];
        }
        return sum;
    }

    public void ValidateIntegrity()
    {
        for (int perspective = 0; perspective < 2; perspective++)
        {
            if (_accumulation[perspective] == null)
            {
                throw new InvalidOperationException($"Accumulation array for perspective {perspective} is null");
            }

            if (_accumulation[perspective].Length != NNUEConstants.L1Size)
            {
                throw new InvalidOperationException($"Accumulation array for perspective {perspective} has incorrect length: {_accumulation[perspective].Length}, expected: {NNUEConstants.L1Size}");
            }
        }
    }
}
