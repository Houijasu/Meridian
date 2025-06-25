#nullable enable

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Meridian.Core.Board;

public readonly struct Bitboard : IEquatable<Bitboard>
{
    private readonly ulong _value;

    public static readonly Bitboard Empty = new(0);
    public static readonly Bitboard Full = new(0xFFFFFFFFFFFFFFFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard(ulong value) => _value = value;

    public ulong Value => _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard operator &(Bitboard left, Bitboard right) => new(left._value & right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard operator |(Bitboard left, Bitboard right) => new(left._value | right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard operator ^(Bitboard left, Bitboard right) => new(left._value ^ right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard operator ~(Bitboard bitboard) => new(~bitboard._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard operator <<(Bitboard bitboard, int shift) => new(bitboard._value << shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard operator >>(Bitboard bitboard, int shift) => new(bitboard._value >> shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Bitboard left, Bitboard right) => left._value == right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Bitboard left, Bitboard right) => left._value != right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ulong(Bitboard bitboard) => bitboard._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Bitboard(ulong value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount() => BitOperations.PopCount(_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TrailingZeroCount() => BitOperations.TrailingZeroCount(_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LeadingZeroCount() => BitOperations.LeadingZeroCount(_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty() => _value == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNotEmpty() => _value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasBit(int square) => (_value & (1UL << square)) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard SetBit(int square) => new(_value | (1UL << square));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard ClearBit(int square) => new(_value & ~(1UL << square));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard ToggleBit(int square) => new(_value ^ (1UL << square));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetLsbIndex() => TrailingZeroCount();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard PopLsb(out int square)
    {
        square = TrailingZeroCount();
        return new(_value & (_value - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard RemoveLsb() => new(_value & (_value - 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard IsolateLsb() => new(_value & (ulong)-(long)_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Pext(ulong mask)
    {
        if (Bmi2.X64.IsSupported)
            return Bmi2.X64.ParallelBitExtract(_value, mask);
        
        ulong result = 0;
        ulong bb = _value;
        for (ulong m = mask, i = 0; m != 0; m &= m - 1, i++)
        {
            if ((bb & (ulong)-(long)m & m) != 0)
                result |= 1UL << (int)i;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Pdep(ulong mask)
    {
        if (Bmi2.X64.IsSupported)
            return Bmi2.X64.ParallelBitDeposit(_value, mask);
        
        ulong result = 0;
        ulong bb = _value;
        for (ulong m = mask; m != 0; m &= m - 1)
        {
            if ((bb & 1) != 0)
                result |= (ulong)-(long)m & m;
            bb >>= 1;
        }
        return result;
    }

    public bool Equals(Bitboard other) => _value == other._value;

    public override bool Equals(object? obj) => obj is Bitboard other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => $"0x{_value:X16}";
}