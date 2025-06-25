#nullable enable

using System.Runtime.CompilerServices;

namespace Meridian.Core.Protocol.UCI;

public static class UciOutput
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Id(string name, string author)
    {
        Console.WriteLine($"id name {name}");
        Console.WriteLine($"id author {author}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UciOk() => Console.WriteLine("uciok");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadyOk() => Console.WriteLine("readyok");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BestMove(string move, string? ponder = null)
    {
        if (string.IsNullOrEmpty(ponder))
            Console.WriteLine($"bestmove {move}");
        else
            Console.WriteLine($"bestmove {move} ponder {ponder}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Info(string message) => Console.WriteLine($"info string {message}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(string message) => Console.WriteLine($"info string ERROR: {message}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(string message) => Console.WriteLine($"info string DEBUG: {message}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Option(string name, string type, string defaultValue, string? min = null, string? max = null)
    {
        var option = $"option name {name} type {type} default {defaultValue}";
        if (!string.IsNullOrEmpty(min))
            option += $" min {min}";
        if (!string.IsNullOrEmpty(max))
            option += $" max {max}";
        Console.WriteLine(option);
    }
}