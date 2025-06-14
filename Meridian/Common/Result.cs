namespace Meridian.Common;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Zero-allocation result type for operations that can fail
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Result<T> where T : struct
{
    private readonly bool _isSuccess;
    private readonly T _value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result(T value, bool isSuccess)
    {
        _value = value;
        _isSuccess = isSuccess;
    }
    
    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isSuccess;
    }
    
    public bool IsFailure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_isSuccess;
    }
    
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Success(T value) => new(value, true);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure() => new(default, false);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T value)
    {
        value = _value;
        return _isSuccess;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault(T defaultValue = default)
    {
        return _isSuccess ? _value : defaultValue;
    }
}

/// <summary>
/// Result type for operations with no return value
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Result
{
    private readonly bool _isSuccess;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result(bool isSuccess)
    {
        _isSuccess = isSuccess;
    }
    
    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isSuccess;
    }
    
    public bool IsFailure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_isSuccess;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Success() => new(true);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure() => new(false);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator bool(Result result) => result._isSuccess;
}