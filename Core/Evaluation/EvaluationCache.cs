namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Cache for full position evaluations to avoid redundant calculations.
/// </summary>
public sealed class EvaluationCache
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct EvalEntry
    {
        public ulong Key;
        public short Score;
    }
    
    private readonly EvalEntry[] entries;
    private readonly int mask;
    
    /// <summary>
    /// Creates a new evaluation cache with the specified size in MB.
    /// </summary>
    public EvaluationCache(int sizeMB = 32)
    {
        int entrySize = Marshal.SizeOf<EvalEntry>();
        int numEntries = (sizeMB * 1024 * 1024) / entrySize;
        
        // Round down to power of 2
        numEntries = 1 << (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)numEntries));
        
        entries = new EvalEntry[numEntries];
        mask = numEntries - 1;
    }
    
    /// <summary>
    /// Probes the evaluation cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Probe(ulong hash, out int score)
    {
        int index = (int)(hash & (ulong)mask);
        ref EvalEntry entry = ref entries[index];
        
        if (entry.Key == hash)
        {
            score = entry.Score;
            return true;
        }
        
        score = 0;
        return false;
    }
    
    /// <summary>
    /// Stores an evaluation in the cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Store(ulong hash, int score)
    {
        int index = (int)(hash & (ulong)mask);
        ref EvalEntry entry = ref entries[index];
        
        entry.Key = hash;
        entry.Score = (short)score;
    }
    
    /// <summary>
    /// Clears all entries in the cache.
    /// </summary>
    public void Clear()
    {
        Array.Clear(entries);
    }
}