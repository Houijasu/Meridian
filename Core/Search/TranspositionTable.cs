namespace Meridian.Core.Search;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
///    Bound type for transposition table entries.
/// </summary>
public enum BoundType : byte
{
   None = 0,
   Exact = 1,
   Lower = 2,
   Upper = 3
}

/// <summary>
///    Transposition table entry.
///    Packed to 16 bytes for cache efficiency.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TTEntry
{
   public ulong Key;
   public Move BestMove;
   public short Score;
   public byte Depth;
   public BoundType Bound;

   public readonly bool IsValid => Key != 0;
}

/// <summary>
///    Transposition table for storing search results.
///    Uses always-replace scheme for simplicity.
/// </summary>
public sealed class TranspositionTable
{
   private readonly TTEntry[] entries;
   private readonly int mask;

   /// <summary>
   ///    Creates a new transposition table with the specified size in MB.
   /// </summary>
   public TranspositionTable(int sizeMB = 128)
   {
      const int entrySize = 16;
      var numEntries = sizeMB * 1024 * 1024 / entrySize;

      numEntries = 1 << 31 - BitOperations.LeadingZeroCount((uint)numEntries);

      entries = new TTEntry[numEntries];
      mask = numEntries - 1;
   }

   /// <summary>
   ///    Clears the transposition table.
   /// </summary>
   public void Clear() => Array.Clear(entries);

   /// <summary>
   ///    Probes the transposition table for a position.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public bool Probe(ulong key, out TTEntry entry)
   {
      var index = (int)(key & (ulong)mask);
      entry = entries[index];
      return entry.IsValid && entry.Key == key;
   }

   /// <summary>
   ///    Stores an entry in the transposition table.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void Store(ulong key, Move bestMove, short score, byte depth, BoundType bound)
   {
      var index = (int)(key & (ulong)mask);

      entries[index] = new TTEntry {
         Key = key,
         BestMove = bestMove,
         Score = score,
         Depth = depth,
         Bound = bound
      };
   }

   /// <summary>
   ///    Gets the fill rate of the transposition table (0-1000 permille).
   /// </summary>
   public int GetHashFull()
   {
      if (entries.Length == 0)
         return 0;
         
      var used = 0;
      var sampleSize = Math.Min(1000, entries.Length);
      
      if (sampleSize == 0)
         return 0;

      for (var i = 0; i < sampleSize; i++)
      {
         if (entries[i].IsValid)
            used++;
      }

      return used * 1000 / sampleSize;
   }

   /// <summary>
   ///    Adjusts mate scores for the current ply.
   ///    Mate scores are stored relative to the root, so we need to adjust them.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static short ScoreToTT(short score, int ply)
   {
      if (score >= SearchConstants.Checkmate - SearchConstants.MaxPly)
         return (short)(score + ply);

      if (score <= -SearchConstants.Checkmate + SearchConstants.MaxPly)
         return (short)(score - ply);

      return score;
   }

   /// <summary>
   ///    Adjusts mate scores from TT for the current ply.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static short ScoreFromTT(short score, int ply)
   {
      if (score >= SearchConstants.Checkmate - SearchConstants.MaxPly)
         return (short)(score - ply);

      if (score <= -SearchConstants.Checkmate + SearchConstants.MaxPly)
         return (short)(score + ply);

      return score;
   }
}
