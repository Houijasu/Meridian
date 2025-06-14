namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class MagicBitboards
{
    private struct MagicEntry
    {
        public ulong Mask;
        public ulong Magic;
        public int Shift;
        public unsafe ulong* Attacks;
    }

    private static readonly MagicEntry[] RookMagics = new MagicEntry[64];
    private static readonly MagicEntry[] BishopMagics = new MagicEntry[64];
    
    private static readonly ulong[] RookAttackTable = new ulong[102400];
    private static readonly ulong[] BishopAttackTable = new ulong[5248];
    
    private static int RookTableIndex;
    private static int BishopTableIndex;

    static MagicBitboards()
    {
        InitializeMagics();
    }

    private static void InitializeMagics()
    {
        InitializeRookMagics();
        InitializeBishopMagics();
    }

    private static unsafe void InitializeRookMagics()
    {
        ReadOnlySpan<ulong> magics = [
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

        fixed (ulong* tablePtr = RookAttackTable)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                ref MagicEntry entry = ref RookMagics[sq];
                entry.Mask = RookMask(sq);
                entry.Magic = magics[sq];
                entry.Shift = 64 - Bitboard.PopCount(entry.Mask);
                entry.Attacks = tablePtr + RookTableIndex;

                int size = 1 << (64 - entry.Shift);
                for (int i = 0; i < size; i++)
                {
                    ulong occupied = Bitboard.Pdep((ulong)i, entry.Mask);
                    int index = (int)((occupied * entry.Magic) >> entry.Shift);
                    entry.Attacks[index] = SlowRookAttacks(sq, occupied);
                }
                RookTableIndex += size;
            }
        }
    }

    private static unsafe void InitializeBishopMagics()
    {
        ReadOnlySpan<ulong> magics = [
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

        fixed (ulong* tablePtr = BishopAttackTable)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                ref MagicEntry entry = ref BishopMagics[sq];
                entry.Mask = BishopMask(sq);
                entry.Magic = magics[sq];
                entry.Shift = 64 - Bitboard.PopCount(entry.Mask);
                entry.Attacks = tablePtr + BishopTableIndex;

                int size = 1 << (64 - entry.Shift);
                for (int i = 0; i < size; i++)
                {
                    ulong occupied = Bitboard.Pdep((ulong)i, entry.Mask);
                    int index = (int)((occupied * entry.Magic) >> entry.Shift);
                    entry.Attacks[index] = SlowBishopAttacks(sq, occupied);
                }
                BishopTableIndex += size;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ulong GetRookAttacks(int square, ulong occupied)
    {
        ref readonly MagicEntry entry = ref RookMagics[square];
        occupied &= entry.Mask;
        occupied *= entry.Magic;
        occupied >>= entry.Shift;
        return entry.Attacks[occupied];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ulong GetBishopAttacks(int square, ulong occupied)
    {
        ref readonly MagicEntry entry = ref BishopMagics[square];
        occupied &= entry.Mask;
        occupied *= entry.Magic;
        occupied >>= entry.Shift;
        return entry.Attacks[occupied];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetQueenAttacks(int square, ulong occupied)
    {
        return GetRookAttacks(square, occupied) | GetBishopAttacks(square, occupied);
    }

    private static ulong RookMask(int square)
    {
        ulong mask = 0;
        int rank = square / 8;
        int file = square % 8;

        for (int r = rank + 1; r < 7; r++)
            mask |= 1UL << (r * 8 + file);
        for (int r = rank - 1; r > 0; r--)
            mask |= 1UL << (r * 8 + file);
        for (int f = file + 1; f < 7; f++)
            mask |= 1UL << (rank * 8 + f);
        for (int f = file - 1; f > 0; f--)
            mask |= 1UL << (rank * 8 + f);

        return mask;
    }

    private static ulong BishopMask(int square)
    {
        ulong mask = 0;
        int rank = square / 8;
        int file = square % 8;

        for (int r = rank + 1, f = file + 1; r < 7 && f < 7; r++, f++)
            mask |= 1UL << (r * 8 + f);
        for (int r = rank - 1, f = file + 1; r > 0 && f < 7; r--, f++)
            mask |= 1UL << (r * 8 + f);
        for (int r = rank + 1, f = file - 1; r < 7 && f > 0; r++, f--)
            mask |= 1UL << (r * 8 + f);
        for (int r = rank - 1, f = file - 1; r > 0 && f > 0; r--, f--)
            mask |= 1UL << (r * 8 + f);

        return mask;
    }

    private static ulong SlowRookAttacks(int square, ulong occupied)
    {
        return Bitboard.RookAttacks(square, occupied);
    }

    private static ulong SlowBishopAttacks(int square, ulong occupied)
    {
        return Bitboard.BishopAttacks(square, occupied);
    }
}