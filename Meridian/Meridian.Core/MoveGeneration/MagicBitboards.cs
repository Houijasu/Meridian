#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;

namespace Meridian.Core.MoveGeneration;

public static class MagicBitboards
{
    private struct MagicEntry
    {
        public Bitboard Mask;
        public ulong Magic;
        public int Shift;
        public Bitboard[] Attacks;
    }
    
    private static readonly MagicEntry[] s_bishopMagics = new MagicEntry[64];
    private static readonly MagicEntry[] s_rookMagics = new MagicEntry[64];
    
    private static readonly ulong[] BishopMagicNumbers = 
    {
        0x0002020202020200UL, 0x0002020202020000UL, 0x0004010202000000UL, 0x0004040080000000UL,
        0x0001104000000000UL, 0x0000821040000000UL, 0x0000410410400000UL, 0x0000104104104000UL,
        0x0000040404040400UL, 0x0000020202020200UL, 0x0000040102020000UL, 0x0000040400800000UL,
        0x0000011040000000UL, 0x0000008210400000UL, 0x0000004104104000UL, 0x0000002082082000UL,
        0x0004000808080800UL, 0x0002000404040400UL, 0x0001000202020200UL, 0x0000800802004000UL,
        0x0000800400A00000UL, 0x0000200100884000UL, 0x0000400082082000UL, 0x0000200041041000UL,
        0x0002080010101000UL, 0x0001040008080800UL, 0x0000208004010400UL, 0x0000404004010200UL,
        0x0000840000802000UL, 0x0000404002011000UL, 0x0000808001041000UL, 0x0000404000820800UL,
        0x0001041000202000UL, 0x0000820800101000UL, 0x0000104400080800UL, 0x0000020080080080UL,
        0x0000404040040100UL, 0x0000808100020100UL, 0x0001010100020800UL, 0x0000808080010400UL,
        0x0000820820004000UL, 0x0000410410002000UL, 0x0000082088001000UL, 0x0000002011000800UL,
        0x0000080100400400UL, 0x0001010101000200UL, 0x0002020202000400UL, 0x0001010101000200UL,
        0x0000410410400000UL, 0x0000208208200000UL, 0x0000002084100000UL, 0x0000000020880000UL,
        0x0000001002020000UL, 0x0000040408020000UL, 0x0004040404040000UL, 0x0002020202020000UL,
        0x0000104104104000UL, 0x0000002082082000UL, 0x0000000020841000UL, 0x0000000000208800UL,
        0x0000000010020200UL, 0x0000000404080200UL, 0x0000040404040400UL, 0x0002020202020200UL
    };
    
    private static readonly ulong[] RookMagicNumbers = 
    {
        0x0080001020400080UL, 0x0040001000200040UL, 0x0080081000200080UL, 0x0080040800100080UL,
        0x0080020400080080UL, 0x0080010200040080UL, 0x0080008001000200UL, 0x0080002040800100UL,
        0x0000800020400080UL, 0x0000400020005000UL, 0x0000801000200080UL, 0x0000800800100080UL,
        0x0000800400080080UL, 0x0000800200040080UL, 0x0000800100020080UL, 0x0000800040800100UL,
        0x0000208000400080UL, 0x0000404000201000UL, 0x0000808010002000UL, 0x0000808008001000UL,
        0x0000808004000800UL, 0x0000808002000400UL, 0x0000010100020004UL, 0x0000020000408104UL,
        0x0000208080004000UL, 0x0000200040005000UL, 0x0000100080200080UL, 0x0000080080100080UL,
        0x0000040080080080UL, 0x0000020080040080UL, 0x0000010080800200UL, 0x0000800080004100UL,
        0x0000204000800080UL, 0x0000200040401000UL, 0x0000100080802000UL, 0x0000080080801000UL,
        0x0000040080800800UL, 0x0000020080800400UL, 0x0000020001010004UL, 0x0000800040800100UL,
        0x0000204000808000UL, 0x0000200040008080UL, 0x0000100020008080UL, 0x0000080010008080UL,
        0x0000040008008080UL, 0x0000020004008080UL, 0x0000010002008080UL, 0x0000004081020004UL,
        0x0000204000800080UL, 0x0000200040008080UL, 0x0000100020008080UL, 0x0000080010008080UL,
        0x0000040008008080UL, 0x0000020004008080UL, 0x0000800100020080UL, 0x0000800041000080UL,
        0x00FFFCDDFCED714AUL, 0x007FFCDDFCED714AUL, 0x003FFFCDFFD88096UL, 0x0000040810002101UL,
        0x0001000204080011UL, 0x0001000204000801UL, 0x0001000082000401UL, 0x0001FFFAABFAD1A2UL
    };
    
    static MagicBitboards()
    {
        InitializeBishopMagics();
        InitializeRookMagics();
    }
    
    private static void InitializeBishopMagics()
    {
        for (var square = 0; square < 64; square++)
        {
            var sq = (Square)square;
            var mask = GenerateBishopMask(sq);
            var magic = BishopMagicNumbers[square];
            var bits = Bitboard.PopCount(mask);
            var shift = 64 - bits;
            var size = 1 << bits;
            
            s_bishopMagics[square] = new MagicEntry
            {
                Mask = mask,
                Magic = magic,
                Shift = shift,
                Attacks = new Bitboard[size]
            };
            
            var occupancies = GenerateOccupancies(mask);
            foreach (var occupancy in occupancies)
            {
                var index = (int)((occupancy.Value * magic) >> shift);
                s_bishopMagics[square].Attacks[index] = GenerateBishopAttacks(sq, occupancy);
            }
        }
    }
    
    private static void InitializeRookMagics()
    {
        for (var square = 0; square < 64; square++)
        {
            var sq = (Square)square;
            var mask = GenerateRookMask(sq);
            var magic = RookMagicNumbers[square];
            var bits = Bitboard.PopCount(mask);
            var shift = 64 - bits;
            var size = 1 << bits;
            
            s_rookMagics[square] = new MagicEntry
            {
                Mask = mask,
                Magic = magic,
                Shift = shift,
                Attacks = new Bitboard[size]
            };
            
            var occupancies = GenerateOccupancies(mask);
            foreach (var occupancy in occupancies)
            {
                var index = (int)((occupancy.Value * magic) >> shift);
                s_rookMagics[square].Attacks[index] = GenerateRookAttacks(sq, occupancy);
            }
        }
    }
    
    private static Bitboard GenerateBishopMask(Square square)
    {
        var mask = Bitboard.Empty;
        var file = square.File();
        var rank = square.Rank();
        
        for (int f = file + 1, r = rank + 1; f < 7 && r < 7; f++, r++)
            mask |= SquareExtensions.FromFileRank(f, r).ToBitboard();
            
        for (int f = file - 1, r = rank + 1; f > 0 && r < 7; f--, r++)
            mask |= SquareExtensions.FromFileRank(f, r).ToBitboard();
            
        for (int f = file + 1, r = rank - 1; f < 7 && r > 0; f++, r--)
            mask |= SquareExtensions.FromFileRank(f, r).ToBitboard();
            
        for (int f = file - 1, r = rank - 1; f > 0 && r > 0; f--, r--)
            mask |= SquareExtensions.FromFileRank(f, r).ToBitboard();
            
        return mask;
    }
    
    private static Bitboard GenerateRookMask(Square square)
    {
        var mask = Bitboard.Empty;
        var file = square.File();
        var rank = square.Rank();
        
        for (var f = file + 1; f < 7; f++)
            mask |= SquareExtensions.FromFileRank(f, rank).ToBitboard();
            
        for (var f = file - 1; f > 0; f--)
            mask |= SquareExtensions.FromFileRank(f, rank).ToBitboard();
            
        for (var r = rank + 1; r < 7; r++)
            mask |= SquareExtensions.FromFileRank(file, r).ToBitboard();
            
        for (var r = rank - 1; r > 0; r--)
            mask |= SquareExtensions.FromFileRank(file, r).ToBitboard();
            
        return mask;
    }
    
    private static Bitboard GenerateBishopAttacks(Square square, Bitboard occupancy)
    {
        var attacks = Bitboard.Empty;
        var file = square.File();
        var rank = square.Rank();
        
        for (int f = file + 1, r = rank + 1; f <= 7 && r <= 7; f++, r++)
        {
            var sq = SquareExtensions.FromFileRank(f, r).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        for (int f = file - 1, r = rank + 1; f >= 0 && r <= 7; f--, r++)
        {
            var sq = SquareExtensions.FromFileRank(f, r).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        for (int f = file + 1, r = rank - 1; f <= 7 && r >= 0; f++, r--)
        {
            var sq = SquareExtensions.FromFileRank(f, r).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        for (int f = file - 1, r = rank - 1; f >= 0 && r >= 0; f--, r--)
        {
            var sq = SquareExtensions.FromFileRank(f, r).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        return attacks;
    }
    
    private static Bitboard GenerateRookAttacks(Square square, Bitboard occupancy)
    {
        var attacks = Bitboard.Empty;
        var file = square.File();
        var rank = square.Rank();
        
        for (var f = file + 1; f <= 7; f++)
        {
            var sq = SquareExtensions.FromFileRank(f, rank).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        for (var f = file - 1; f >= 0; f--)
        {
            var sq = SquareExtensions.FromFileRank(f, rank).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        for (var r = rank + 1; r <= 7; r++)
        {
            var sq = SquareExtensions.FromFileRank(file, r).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        for (var r = rank - 1; r >= 0; r--)
        {
            var sq = SquareExtensions.FromFileRank(file, r).ToBitboard();
            attacks |= sq;
            if ((occupancy & sq).IsNotEmpty()) break;
        }
        
        return attacks;
    }
    
    private static List<Bitboard> GenerateOccupancies(Bitboard mask)
    {
        var occupancies = new List<Bitboard>();
        var n = Bitboard.PopCount(mask);
        var patterns = 1 << n;
        
        for (var pattern = 0; pattern < patterns; pattern++)
        {
            var occupancy = Bitboard.Empty;
            var tempMask = mask;
            
            for (var i = 0; i < n; i++)
            {
                if ((pattern & (1 << i)) != 0)
                {
                    var lsb = tempMask.GetLsbIndex();
                    occupancy |= ((Square)lsb).ToBitboard();
                }
                tempMask = tempMask.RemoveLsb();
            }
            
            occupancies.Add(occupancy);
        }
        
        return occupancies;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard GetBishopAttacks(Square square, Bitboard occupancy)
    {
        ref var entry = ref s_bishopMagics[(int)square];
        occupancy &= entry.Mask;
        var index = (int)((occupancy.Value * entry.Magic) >> entry.Shift);
        return entry.Attacks[index];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard GetRookAttacks(Square square, Bitboard occupancy)
    {
        ref var entry = ref s_rookMagics[(int)square];
        occupancy &= entry.Mask;
        var index = (int)((occupancy.Value * entry.Magic) >> entry.Shift);
        return entry.Attacks[index];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard GetQueenAttacks(Square square, Bitboard occupancy) =>
        GetBishopAttacks(square, occupancy) | GetRookAttacks(square, occupancy);
}