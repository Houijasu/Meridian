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
    }
    
    public void AddPiece(int color, int piece, int square, int kingSquare, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        int weightIndex = NNUEConstants.FeatureWeightIndex(piece, square, kingSquare, color);
        
        if (weightIndex < 0 || weightIndex + NNUEConstants.L1Size > weights.Length)
        {
            throw new IndexOutOfRangeException($"Weight index {weightIndex} out of bounds. Array length: {weights.Length}, piece: {piece}, square: {square}, kingSquare: {kingSquare}");
        }
        
        unsafe
        {
            fixed (short* acc = &_accumulation[color][0])
            fixed (short* w = &weights[weightIndex])
            {
                AddFeature(acc, w);
            }
        }
    }
    
    public void RemovePiece(int color, int piece, int square, int kingSquare, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        int weightIndex = NNUEConstants.FeatureWeightIndex(piece, square, kingSquare, color);
        
        if (weightIndex < 0 || weightIndex + NNUEConstants.L1Size > weights.Length)
        {
            throw new IndexOutOfRangeException($"Weight index {weightIndex} out of bounds. Array length: {weights.Length}, piece: {piece}, square: {square}, kingSquare: {kingSquare}");
        }
        
        unsafe
        {
            fixed (short* acc = &_accumulation[color][0])
            fixed (short* w = &weights[weightIndex])
            {
                SubtractFeature(acc, w);
            }
        }
    }
    
    public void MovePiece(int color, int piece, int from, int to, int kingSquare, short[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        int fromWeightIndex = NNUEConstants.FeatureWeightIndex(piece, from, kingSquare, color);
        int toWeightIndex = NNUEConstants.FeatureWeightIndex(piece, to, kingSquare, color);
        
        if (fromWeightIndex < 0 || fromWeightIndex + NNUEConstants.L1Size > weights.Length ||
            toWeightIndex < 0 || toWeightIndex + NNUEConstants.L1Size > weights.Length)
        {
            throw new IndexOutOfRangeException($"Weight indices out of bounds. From: {fromWeightIndex}, To: {toWeightIndex}, Array length: {weights.Length}");
        }
        
        unsafe
        {
            fixed (short* acc = &_accumulation[color][0])
            fixed (short* wFrom = &weights[fromWeightIndex])
            fixed (short* wTo = &weights[toWeightIndex])
            {
                SubtractFeature(acc, wFrom);
                AddFeature(acc, wTo);
            }
        }
    }
    
    public short[] GetAccumulation(int perspective)
    {
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void AddFeature(short* acc, short* weights)
    {
        int chunks = NNUEConstants.L1Size / 16;
        for (int i = 0; i < chunks; i++)
        {
            if (Avx2.IsSupported)
            {
                var accVec = Avx.LoadVector256(acc + i * 16);
                var weightVec = Avx.LoadVector256(weights + i * 16);
                var result = Avx2.Add(accVec, weightVec);
                Avx.Store(acc + i * 16, result);
            }
            else
            {
                for (int j = 0; j < 16; j++)
                {
                    acc[i * 16 + j] += weights[i * 16 + j];
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void SubtractFeature(short* acc, short* weights)
    {
        int chunks = NNUEConstants.L1Size / 16;
        for (int i = 0; i < chunks; i++)
        {
            if (Avx2.IsSupported)
            {
                var accVec = Avx.LoadVector256(acc + i * 16);
                var weightVec = Avx.LoadVector256(weights + i * 16);
                var result = Avx2.Subtract(accVec, weightVec);
                Avx.Store(acc + i * 16, result);
            }
            else
            {
                for (int j = 0; j < 16; j++)
                {
                    acc[i * 16 + j] -= weights[i * 16 + j];
                }
            }
        }
    }
}