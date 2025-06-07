namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Hash table for caching pawn structure evaluations.
/// Pawn structure changes slowly, so caching provides significant speedup.
/// </summary>
public sealed class PawnHashTable
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PawnHashEntry
    {
        public ulong Key;
        public short MiddlegameScore;
        public short EndgameScore;
        public ulong PassedPawns;
    }
    
    private readonly PawnHashEntry[] entries;
    private readonly int mask;
    
    /// <summary>
    /// Creates a new pawn hash table with the specified size in MB.
    /// </summary>
    public PawnHashTable(int sizeMB = 16)
    {
        int entrySize = Marshal.SizeOf<PawnHashEntry>();
        int numEntries = (sizeMB * 1024 * 1024) / entrySize;
        
        // Round down to power of 2
        numEntries = 1 << (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)numEntries));
        
        entries = new PawnHashEntry[numEntries];
        mask = numEntries - 1;
    }
    
    /// <summary>
    /// Probes the pawn hash table for a cached evaluation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Probe(ulong pawnHash, out int mgScore, out int egScore, out ulong passedPawns)
    {
        int index = (int)(pawnHash & (ulong)mask);
        ref PawnHashEntry entry = ref entries[index];
        
        if (entry.Key == pawnHash)
        {
            mgScore = entry.MiddlegameScore;
            egScore = entry.EndgameScore;
            passedPawns = entry.PassedPawns;
            return true;
        }
        
        mgScore = 0;
        egScore = 0;
        passedPawns = 0;
        return false;
    }
    
    /// <summary>
    /// Stores a pawn structure evaluation in the hash table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Store(ulong pawnHash, int mgScore, int egScore, ulong passedPawns)
    {
        int index = (int)(pawnHash & (ulong)mask);
        ref PawnHashEntry entry = ref entries[index];
        
        entry.Key = pawnHash;
        entry.MiddlegameScore = (short)mgScore;
        entry.EndgameScore = (short)egScore;
        entry.PassedPawns = passedPawns;
    }
    
    /// <summary>
    /// Clears all entries in the hash table.
    /// </summary>
    public void Clear()
    {
        Array.Clear(entries);
    }
}