namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TTEntry
{
    public ulong Hash;
    public uint MoveData; // Packed move data
    public short Score;
    public byte Depth;
    public byte Flags;
    
    public Move Move
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new Move(MoveData);
    }
}

public enum TTFlags : byte
{
    None = 0,
    Exact = 1,
    LowerBound = 2,
    UpperBound = 3
}

public sealed class TranspositionTable
{
    private const int DefaultSizeMB = 128;
    private readonly TTEntry[] _entries;
    private readonly uint _mask;
    
    // Statistics
    public ulong Hits { get; private set; }
    public ulong Stores { get; private set; }
    public ulong Collisions { get; private set; }
    
    public TranspositionTable(int sizeMB = DefaultSizeMB)
    {
        // Calculate number of entries
        int entrySize = Unsafe.SizeOf<TTEntry>();
        int numEntries = (sizeMB * 1024 * 1024) / entrySize;
        
        // Round down to power of 2 for fast indexing
        numEntries = 1 << (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)numEntries));
        
        _entries = new TTEntry[numEntries];
        _mask = (uint)(numEntries - 1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Probe(ulong hash, int depth, int alpha, int beta, out TTEntry entry)
    {
        uint index = (uint)hash & _mask;
        entry = _entries[index];
        
        if (entry.Hash != hash)
        {
            return false;
        }
        
        Hits++;
        
        // Check if stored depth is sufficient
        if (entry.Depth < depth)
        {
            return false;
        }
        
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Store(ulong hash, Move move, int score, int depth, TTFlags flags)
    {
        uint index = (uint)hash & _mask;
        ref TTEntry entry = ref _entries[index];
        
        // Replace if empty, same position, or deeper search
        if (entry.Hash == 0 || entry.Hash == hash || entry.Depth <= depth)
        {
            if (entry.Hash != 0 && entry.Hash != hash)
            {
                Collisions++;
            }
            
            entry.Hash = hash;
            entry.MoveData = move.Data;
            entry.Score = (short)score;
            entry.Depth = (byte)depth;
            entry.Flags = (byte)flags;
            
            Stores++;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Move GetBestMove(ulong hash)
    {
        uint index = (uint)hash & _mask;
        ref TTEntry entry = ref _entries[index];
        
        if (entry.Hash == hash)
        {
            return entry.Move;
        }
        
        return default;
    }
    
    public void Clear()
    {
        Array.Clear(_entries);
        Hits = 0;
        Stores = 0;
        Collisions = 0;
    }
    
    public double FillRate()
    {
        int used = 0;
        for (int i = 0; i < Math.Min(1000, _entries.Length); i++)
        {
            if (_entries[i].Hash != 0)
                used++;
        }
        return used / 10.0; // Percentage based on sample
    }
}