namespace Meridian.Common;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Zero-allocation optional value type for performance-critical code
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Option<T> where T : struct
{
    private readonly bool _hasValue;
    private readonly T _value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Option(T value)
    {
        _hasValue = true;
        _value = value;
    }
    
    public bool HasValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasValue;
    }
    
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> Some(T value) => new(value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> None() => default;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T value)
    {
        value = _value;
        return _hasValue;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault(T defaultValue = default)
    {
        return _hasValue ? _value : defaultValue;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Option<T>(T value) => Some(value);
}