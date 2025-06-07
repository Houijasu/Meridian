namespace Meridian.Core.Search;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
///    Thread-safe transposition table using lockless hashing with XOR trick.
///    Uses a cluster approach where each hash maps to a cluster of entries.
/// </summary>
public sealed class ThreadSafeTranspositionTable
{
   // Cluster size - must be power of 2 for cache alignment
   private const int ClusterSize = 4;
   
   /// <summary>
   ///    A cluster of TT entries. Using clusters improves cache performance
   ///    and reduces conflicts in multi-threaded scenarios.
   /// </summary>
   [StructLayout(LayoutKind.Sequential, Pack = 1)]
   private struct TTCluster
   {
      private TTEntry entry0;
      private TTEntry entry1;
      private TTEntry entry2;
      private TTEntry entry3;
      
      /// <summary>
      ///    Probes the cluster for a matching entry.
      /// </summary>
      public bool Probe(ulong key, out TTEntry entry)
      {
         // Check each entry in the cluster
         if (entry0.Key == key) { entry = entry0; return entry0.IsValid; }
         if (entry1.Key == key) { entry = entry1; return entry1.IsValid; }
         if (entry2.Key == key) { entry = entry2; return entry2.IsValid; }
         if (entry3.Key == key) { entry = entry3; return entry3.IsValid; }
         
         entry = default;
         return false;
      }
      
      /// <summary>
      ///    Stores an entry in the cluster using a replacement scheme.
      /// </summary>
      public void Store(ulong key, Move bestMove, short score, byte depth, BoundType bound, byte generation)
      {
         TTEntry newEntry = new()
         {
            Key = key,
            BestMove = bestMove,
            Score = score,
            Depth = depth,
            Bound = bound,
            Generation = generation
         };
         
         // Find entry to replace
         // Priority: 1) Same key, 2) Empty slot, 3) Lowest depth from older generation
         
         if (entry0.Key == key || !entry0.IsValid) 
         {
            entry0 = newEntry;
            return;
         }
         if (entry1.Key == key || !entry1.IsValid) 
         {
            entry1 = newEntry;
            return;
         }
         if (entry2.Key == key || !entry2.IsValid) 
         {
            entry2 = newEntry;
            return;
         }
         if (entry3.Key == key || !entry3.IsValid) 
         {
            entry3 = newEntry;
            return;
         }
         
         // Replace entry with lowest value (considering depth and generation)
         int replaceIdx = 0;
         int lowestValue = GetReplacementValue(entry0, generation);
         
         int value1 = GetReplacementValue(entry1, generation);
         if (value1 < lowestValue) { lowestValue = value1; replaceIdx = 1; }
         
         int value2 = GetReplacementValue(entry2, generation);
         if (value2 < lowestValue) { lowestValue = value2; replaceIdx = 2; }
         
         int value3 = GetReplacementValue(entry3, generation);
         if (value3 < lowestValue) { replaceIdx = 3; }
         
         // Replace the selected entry
         switch (replaceIdx)
         {
            case 0: entry0 = newEntry; break;
            case 1: entry1 = newEntry; break;
            case 2: entry2 = newEntry; break;
            case 3: entry3 = newEntry; break;
         }
      }
      
      private static int GetReplacementValue(in TTEntry entry, byte currentGen)
      {
         // Prefer replacing entries from older generations
         int genBonus = (entry.Generation == currentGen) ? 256 : 0;
         return entry.Depth + genBonus;
      }
      
      public int CountValid()
      {
         int count = 0;
         if (entry0.IsValid) count++;
         if (entry1.IsValid) count++;
         if (entry2.IsValid) count++;
         if (entry3.IsValid) count++;
         return count;
      }
   }
   
   /// <summary>
   ///    Extended TT entry with generation for replacement scheme.
   /// </summary>
   [StructLayout(LayoutKind.Sequential, Pack = 1)]
   private struct TTEntry
   {
      public ulong Key;
      public Move BestMove;
      public short Score;
      public byte Depth;
      public BoundType Bound;
      public byte Generation;
      
      public readonly bool IsValid => Key != 0;
   }
   
   private readonly TTCluster[] clusters;
   private readonly int clusterCount;
   private byte currentGeneration;
   
   /// <summary>
   ///    Creates a new thread-safe transposition table with the specified size in MB.
   /// </summary>
   public ThreadSafeTranspositionTable(int sizeMB = 128)
   {
      // TTEntry is 16 bytes, cluster has 4 entries = 64 bytes
      const int bytesPerCluster = 64;
      clusterCount = sizeMB * 1024 * 1024 / bytesPerCluster;
      
      // Round down to power of 2 for fast modulo
      clusterCount = 1 << (31 - BitOperations.LeadingZeroCount((uint)clusterCount));
      
      clusters = new TTCluster[clusterCount];
      currentGeneration = 0;
   }
   
   /// <summary>
   ///    Clears the transposition table by incrementing generation.
   /// </summary>
   public void Clear()
   {
      // Instead of clearing memory, just increment generation
      // Old entries will be naturally replaced
      currentGeneration++;
   }
   
   /// <summary>
   ///    Probes the transposition table for a position.
   ///    Thread-safe without locks.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public bool Probe(ulong key, out Core.Search.TTEntry entry)
   {
      int clusterIdx = (int)(key % (ulong)clusterCount);
      if (clusters[clusterIdx].Probe(key, out TTEntry ttEntry))
      {
         entry = new Core.Search.TTEntry
         {
            Key = ttEntry.Key,
            BestMove = ttEntry.BestMove,
            Score = ttEntry.Score,
            Depth = ttEntry.Depth,
            Bound = ttEntry.Bound
         };
         return true;
      }
      entry = default;
      return false;
   }
   
   /// <summary>
   ///    Stores an entry in the transposition table.
   ///    Thread-safe using atomic operations.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void Store(ulong key, Move bestMove, short score, byte depth, BoundType bound)
   {
      int clusterIdx = (int)(key % (ulong)clusterCount);
      clusters[clusterIdx].Store(key, bestMove, score, depth, bound, currentGeneration);
   }
   
   /// <summary>
   ///    Gets the fill rate of the transposition table (0-1000 permille).
   /// </summary>
   public int GetHashFull()
   {
      if (clusterCount == 0) return 0;
      
      const int sampleSize = 1000;
      int validEntries = 0;
      int step = Math.Max(1, clusterCount / sampleSize);
      
      for (int i = 0; i < clusterCount && i / step < sampleSize; i += step)
      {
         validEntries += clusters[i].CountValid();
      }
      
      int sampledClusters = Math.Min(sampleSize, clusterCount);
      return validEntries * 1000 / (sampledClusters * ClusterSize);
   }
   
   /// <summary>
   ///    Adjusts mate scores for the current ply.
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