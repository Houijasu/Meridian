namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Magic bitboard implementation for fast sliding piece attack generation.
///    Uses perfect hashing to achieve O(1) lookup time.
/// </summary>
public static class MagicBitboards
{
    /// <summary>
    ///    Magic numbers for rook attacks (found through exhaustive search).
    /// </summary>
    private static readonly ulong[] RookMagics = [
      0x8a80104000800020UL, 0x140002000100040UL, 0x2801880a0017001UL, 0x100081001000420UL,
      0x200020010080420UL, 0x3001c0002010008UL, 0x8480008002000100UL, 0x2080088004402900UL,
      0x800098204000UL, 0x2024401000200040UL, 0x100802000801000UL, 0x120800800801000UL,
      0x208808088000400UL, 0x2802200800400UL, 0x2200800100020080UL, 0x801000060821100UL,
      0x80044006422000UL, 0x100808020004000UL, 0x12108a0010204200UL, 0x140848010000802UL,
      0x481828014002800UL, 0x8094004002004100UL, 0x4010040010010802UL, 0x20008806104UL,
      0x100400080208000UL, 0x2040002120081000UL, 0x21200680100081UL, 0x20100080080080UL,
      0x2000a00200410UL, 0x20080800400UL, 0x80088400100102UL, 0x80004600042881UL,
      0x4040008040800020UL, 0x440003000200801UL, 0x4200011004500UL, 0x188020010100100UL,
      0x14800401802800UL, 0x2080040080800200UL, 0x124080204001001UL, 0x200046502000484UL,
      0x480400080088020UL, 0x1000422010034000UL, 0x30200100110040UL, 0x100021010009UL,
      0x2002080100110004UL, 0x202008004008002UL, 0x20020004010100UL, 0x2048440040820001UL,
      0x101002200408200UL, 0x40802000401080UL, 0x4008142004410100UL, 0x2060820c0120200UL,
      0x1001004080100UL, 0x20c020080040080UL, 0x2935610830022400UL, 0x44440041009200UL,
      0x280001040802101UL, 0x2100190040002085UL, 0x80c0084100102001UL, 0x4024081001000421UL,
      0x20030a0244872UL, 0x12001008414402UL, 0x2006104900a0804UL, 0x1004081002402UL
   ];

    /// <summary>
    ///    Magic numbers for bishop attacks.
    /// </summary>
    private static readonly ulong[] BishopMagics = [
      0x40040844404084UL, 0x2004208a004208UL, 0x10190041080202UL, 0x108060845042010UL,
      0x581104180800210UL, 0x2112080446200010UL, 0x1080820820060210UL, 0x3c0808410220200UL,
      0x4050404440404UL, 0x21001420088UL, 0x24d0080801082102UL, 0x1020a0a020400UL,
      0x40308200402UL, 0x4011002100800UL, 0x401484104104005UL, 0x801010402020200UL,
      0x400210c3880100UL, 0x404022024108200UL, 0x810018200204102UL, 0x4002801a02003UL,
      0x85040820080400UL, 0x810102c808880400UL, 0xe900410884800UL, 0x8002020480840102UL,
      0x220200865090201UL, 0x2010100a02021202UL, 0x152048408022401UL, 0x20080002081110UL,
      0x4001001021004000UL, 0x800040400a011002UL, 0xe4004081011002UL, 0x1c004001012080UL,
      0x8004200962a00220UL, 0x8422100208500202UL, 0x2000402200300c08UL, 0x8646020080080080UL,
      0x80020a0200100808UL, 0x2010004880111000UL, 0x623000a080011400UL, 0x42008c0340209202UL,
      0x209188240001000UL, 0x400408a884001800UL, 0x110400a6080400UL, 0x1840060a44020800UL,
      0x90080104000041UL, 0x201011000808101UL, 0x1a2208080504f080UL, 0x8012020600211212UL,
      0x500861011240000UL, 0x180806108200800UL, 0x4000020e01040044UL, 0x300000261044000aUL,
      0x802241102020002UL, 0x20906061210001UL, 0x5a84841004010310UL, 0x4010801011c04UL,
      0xa010109502200UL, 0x4a02012000UL, 0x500201010098b028UL, 0x8040002811040900UL,
      0x28000010020204UL, 0x6000020202d0240UL, 0x8918844842082200UL, 0x4010011029020020UL
   ];

    /// <summary>
    ///    Bit shifts for rook magic indexing.
    /// </summary>
    private static readonly int[] RookShifts = [
      52, 53, 53, 53, 53, 53, 53, 52,
      53, 54, 54, 54, 54, 54, 54, 53,
      53, 54, 54, 54, 54, 54, 54, 53,
      53, 54, 54, 54, 54, 54, 54, 53,
      53, 54, 54, 54, 54, 54, 54, 53,
      53, 54, 54, 54, 54, 54, 54, 53,
      53, 54, 54, 54, 54, 54, 54, 53,
      52, 53, 53, 53, 53, 53, 53, 52
   ];

    /// <summary>
    ///    Bit shifts for bishop magic indexing.
    /// </summary>
    private static readonly int[] BishopShifts = [
      58, 59, 59, 59, 59, 59, 59, 58,
      59, 59, 59, 59, 59, 59, 59, 59,
      59, 59, 57, 57, 57, 57, 59, 59,
      59, 59, 57, 55, 55, 57, 59, 59,
      59, 59, 57, 55, 55, 57, 59, 59,
      59, 59, 57, 57, 57, 57, 59, 59,
      59, 59, 59, 59, 59, 59, 59, 59,
      58, 59, 59, 59, 59, 59, 59, 58
   ];

    /// <summary>
    ///    Rook occupancy masks (squares that can block rook movement).
    /// </summary>
    private static readonly ulong[] RookMasks = InitializeRookMasks();

    /// <summary>
    ///    Bishop occupancy masks.
    /// </summary>
    private static readonly ulong[] BishopMasks = InitializeBishopMasks();

    /// <summary>
    ///    Rook attack lookup tables.
    /// </summary>
    private static readonly ulong[][] RookAttacks = InitializeRookAttacks();

    /// <summary>
    ///    Bishop attack lookup tables.
    /// </summary>
    private static readonly ulong[][] BishopAttacks = InitializeBishopAttacks();

    /// <summary>
    ///    Gets rook attacks for a given square and occupancy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetRookAttacks(int square, ulong occupancy)
   {
      occupancy &= RookMasks[square];
      occupancy *= RookMagics[square];
      occupancy >>= RookShifts[square];
      return RookAttacks[square][occupancy];
   }

    /// <summary>
    ///    Gets bishop attacks for a given square and occupancy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetBishopAttacks(int square, ulong occupancy)
   {
      occupancy &= BishopMasks[square];
      occupancy *= BishopMagics[square];
      occupancy >>= BishopShifts[square];
      return BishopAttacks[square][occupancy];
   }

    /// <summary>
    ///    Gets queen attacks (combination of rook and bishop).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetQueenAttacks(int square, ulong occupancy) =>
      GetRookAttacks(square, occupancy) | GetBishopAttacks(square, occupancy);

    /// <summary>
    ///    Initializes rook occupancy masks.
    /// </summary>
    private static ulong[] InitializeRookMasks()
   {
      var masks = new ulong[64];

      for (var square = 0; square < 64; square++)
      {
         ulong mask = 0;
         var rank = square / 8;
         var file = square % 8;

         // North
         for (var r = rank + 1; r < 7; r++)
         {
            mask |= 1UL << r * 8 + file;
         }

         // South
         for (var r = rank - 1; r > 0; r--)
         {
            mask |= 1UL << r * 8 + file;
         }

         // East
         for (var f = file + 1; f < 7; f++)
         {
            mask |= 1UL << rank * 8 + f;
         }

         // West
         for (var f = file - 1; f > 0; f--)
         {
            mask |= 1UL << rank * 8 + f;
         }

         masks[square] = mask;
      }

      return masks;
   }

    /// <summary>
    ///    Initializes bishop occupancy masks.
    /// </summary>
    private static ulong[] InitializeBishopMasks()
   {
      var masks = new ulong[64];

      for (var square = 0; square < 64; square++)
      {
         ulong mask = 0;
         var rank = square / 8;
         var file = square % 8;

         // North-East
         for (int r = rank + 1, f = file + 1; r < 7 && f < 7; r++, f++)
         {
            mask |= 1UL << r * 8 + f;
         }

         // North-West
         for (int r = rank + 1, f = file - 1; r < 7 && f > 0; r++, f--)
         {
            mask |= 1UL << r * 8 + f;
         }

         // South-East
         for (int r = rank - 1, f = file + 1; r > 0 && f < 7; r--, f++)
         {
            mask |= 1UL << r * 8 + f;
         }

         // South-West
         for (int r = rank - 1, f = file - 1; r > 0 && f > 0; r--, f--)
         {
            mask |= 1UL << r * 8 + f;
         }

         masks[square] = mask;
      }

      return masks;
   }

    /// <summary>
    ///    Generates rook attacks for a given square and occupancy pattern.
    /// </summary>
    private static ulong GenerateRookAttacks(int square, ulong occupancy)
   {
      ulong attacks = 0;
      var rank = square / 8;
      var file = square % 8;

      // North
      for (var r = rank + 1; r < 8; r++)
      {
         attacks |= 1UL << r * 8 + file;
         if ((occupancy & 1UL << r * 8 + file) != 0) break;
      }

      // South
      for (var r = rank - 1; r >= 0; r--)
      {
         attacks |= 1UL << r * 8 + file;
         if ((occupancy & 1UL << r * 8 + file) != 0) break;
      }

      // East
      for (var f = file + 1; f < 8; f++)
      {
         attacks |= 1UL << rank * 8 + f;
         if ((occupancy & 1UL << rank * 8 + f) != 0) break;
      }

      // West
      for (var f = file - 1; f >= 0; f--)
      {
         attacks |= 1UL << rank * 8 + f;
         if ((occupancy & 1UL << rank * 8 + f) != 0) break;
      }

      return attacks;
   }

    /// <summary>
    ///    Generates bishop attacks for a given square and occupancy pattern.
    /// </summary>
    private static ulong GenerateBishopAttacks(int square, ulong occupancy)
   {
      ulong attacks = 0;
      var rank = square / 8;
      var file = square % 8;

      // North-East
      for (int r = rank + 1, f = file + 1; r < 8 && f < 8; r++, f++)
      {
         attacks |= 1UL << r * 8 + f;
         if ((occupancy & 1UL << r * 8 + f) != 0) break;
      }

      // North-West
      for (int r = rank + 1, f = file - 1; r < 8 && f >= 0; r++, f--)
      {
         attacks |= 1UL << r * 8 + f;
         if ((occupancy & 1UL << r * 8 + f) != 0) break;
      }

      // South-East
      for (int r = rank - 1, f = file + 1; r >= 0 && f < 8; r--, f++)
      {
         attacks |= 1UL << r * 8 + f;
         if ((occupancy & 1UL << r * 8 + f) != 0) break;
      }

      // South-West
      for (int r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
      {
         attacks |= 1UL << r * 8 + f;
         if ((occupancy & 1UL << r * 8 + f) != 0) break;
      }

      return attacks;
   }

    /// <summary>
    ///    Initializes rook attack tables using magic bitboards.
    /// </summary>
    private static ulong[][] InitializeRookAttacks()
   {
      var attacks = new ulong[64][];

      for (var square = 0; square < 64; square++)
      {
         var bits = Bitboard.PopCount(RookMasks[square]);
         var size = 1 << bits;
         attacks[square] = new ulong[size];

         // Generate all possible occupancy variations
         for (var index = 0; index < size; index++)
         {
            var occupancy = GenerateOccupancy(index, bits, RookMasks[square]);
            var magicIndex = (int)(occupancy * RookMagics[square] >> RookShifts[square]);
            attacks[square][magicIndex] = GenerateRookAttacks(square, occupancy);
         }
      }

      return attacks;
   }

    /// <summary>
    ///    Initializes bishop attack tables using magic bitboards.
    /// </summary>
    private static ulong[][] InitializeBishopAttacks()
   {
      var attacks = new ulong[64][];

      for (var square = 0; square < 64; square++)
      {
         var bits = Bitboard.PopCount(BishopMasks[square]);
         var size = 1 << bits;
         attacks[square] = new ulong[size];

         // Generate all possible occupancy variations
         for (var index = 0; index < size; index++)
         {
            var occupancy = GenerateOccupancy(index, bits, BishopMasks[square]);
            var magicIndex = (int)(occupancy * BishopMagics[square] >> BishopShifts[square]);
            attacks[square][magicIndex] = GenerateBishopAttacks(square, occupancy);
         }
      }

      return attacks;
   }

    /// <summary>
    ///    Generates an occupancy variation from an index.
    /// </summary>
    private static ulong GenerateOccupancy(int index, int bits, ulong mask)
   {
      ulong occupancy = 0;

      for (var i = 0; i < bits; i++)
      {
         var square = Bitboard.PopLsb(ref mask);

         if ((index & 1 << i) != 0)
            occupancy |= 1UL << square;
      }

      return occupancy;
   }
}
