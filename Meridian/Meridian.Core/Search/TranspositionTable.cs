#nullable enable

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Meridian.Core.Board;

namespace Meridian.Core.Search;

public enum NodeType
{
    Exact = 0,
    LowerBound = 1,
    UpperBound = 2
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TTEntry : IEquatable<TTEntry>
{
    public ulong Key;
    public short Score;
    public Move BestMove;
    public byte Depth;
    public byte Type;
    public byte Age;
    
    public override readonly bool Equals(object? obj) => obj is TTEntry other && 
        Key == other.Key && Score == other.Score && BestMove == other.BestMove && 
        Depth == other.Depth && Type == other.Type && Age == other.Age;
    
    public override readonly int GetHashCode() => HashCode.Combine(Key, Score, BestMove, Depth, Type, Age);
    
    public static bool operator ==(TTEntry left, TTEntry right) => left.Equals(right);
    
    public static bool operator !=(TTEntry left, TTEntry right) => !left.Equals(right);
    
    public readonly bool Equals(TTEntry other) => 
        Key == other.Key && Score == other.Score && BestMove == other.BestMove && 
        Depth == other.Depth && Type == other.Type && Age == other.Age;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsValid(ulong zobristKey) => Key == zobristKey;
}

public sealed class TranspositionTable
{
    private readonly TTEntry[] _entries;
    private readonly int _sizeMask;
    private int _currentAge;
    
    public TranspositionTable(int sizeMb = 128)
    {
        var entrySize = Marshal.SizeOf<TTEntry>();
        var numEntries = (sizeMb * 1024 * 1024) / entrySize;
        
        // Round down to power of 2 for fast indexing
        numEntries = 1 << (31 - BitOperations.LeadingZeroCount((uint)numEntries));
        
        _entries = new TTEntry[numEntries];
        _sizeMask = numEntries - 1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Probe(ulong key, int depth, int alpha, int beta, int ply, out int score, out Move bestMove)
    {
        var index = (int)(key & (ulong)_sizeMask);
        var entry = _entries[index];
        
        bestMove = Move.None;
        score = 0;
        
        if (!entry.IsValid(key))
            return false;
            
        bestMove = entry.BestMove;
        
        if (entry.Depth < depth)
            return false;
            
        score = entry.Score;
        
        // Adjust mate scores relative to current ply
        if (score > SearchConstants.MateInMaxPly)
            score -= ply;
        else if (score < -SearchConstants.MateInMaxPly)
            score += ply;
        
        switch ((NodeType)entry.Type)
        {
            case NodeType.Exact:
                return true;
            case NodeType.LowerBound:
                if (score >= beta)
                {
                    score = beta;
                    return true;
                }
                break;
            case NodeType.UpperBound:
                if (score <= alpha)
                {
                    score = alpha;
                    return true;
                }
                break;
        }
        
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Store(ulong key, int score, Move bestMove, int depth, NodeType type, int ply)
    {
        var index = (int)(key & (ulong)_sizeMask);
        ref var entry = ref _entries[index];
        
        // Lock-free update using a local copy
        var currentAge = _currentAge;
        var oldEntry = entry;
        
        // Improved replacement strategy
        if (oldEntry.IsValid(key))
        {
            // Same position: replace if deeper or same depth with exact bound
            if (depth < oldEntry.Depth && type != NodeType.Exact)
                return;
        }
        else if (oldEntry.Age == currentAge && oldEntry.Depth > depth + 3)
        {
            // Different position from current search: keep if significantly deeper
            return;
        }
        
        // Adjust mate scores relative to root
        if (score > SearchConstants.MateInMaxPly)
            score += ply;
        else if (score < -SearchConstants.MateInMaxPly)
            score -= ply;
        
        // Create new entry
        var newEntry = new TTEntry
        {
            Key = key,
            Score = (short)Math.Clamp(score, short.MinValue, short.MaxValue),
            BestMove = bestMove,
            Depth = (byte)Math.Min(depth, 255),
            Type = (byte)type,
            Age = (byte)currentAge
        };
        
        // Atomic update - write is atomic for properly aligned structs
        entry = newEntry;
    }
    
    public void Clear()
    {
        Array.Clear(_entries);
        Interlocked.Exchange(ref _currentAge, 0);
    }
    
    public void NewSearch()
    {
        Interlocked.Increment(ref _currentAge);
    }
    
    public int Usage()
    {
        var used = 0;
        var sample = Math.Min(1000, _entries.Length);
        var currentAge = _currentAge;
        
        for (var i = 0; i < sample; i++)
        {
            var entry = _entries[i]; // Local copy for thread safety
            if (entry.Age == currentAge && entry.Key != 0)
                used++;
        }
        
        return used * 1000 / sample;
    }
    
    public int SizeMb => (_entries.Length * Marshal.SizeOf<TTEntry>()) / (1024 * 1024);
}