#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;

namespace Meridian.Core.MoveGeneration;

public static class AttackTables
{
    private static readonly Bitboard[] s_pawnAttacks = new Bitboard[128];
    private static readonly Bitboard[] s_knightAttacks = new Bitboard[64];
    private static readonly Bitboard[] s_kingAttacks = new Bitboard[64];
    
    private static readonly Bitboard[] s_fileMasks = new Bitboard[8];
    private static readonly Bitboard[] s_rankMasks = new Bitboard[8];
    private static readonly Bitboard[] s_diagonalMasks = new Bitboard[15];
    private static readonly Bitboard[] s_antiDiagonalMasks = new Bitboard[15];
    
    private static readonly Bitboard[] s_rayAttacks = new Bitboard[64 * 8];
    
    public static readonly Bitboard FileA = new(0x0101010101010101UL);
    public static readonly Bitboard FileB = new(0x0202020202020202UL);
    public static readonly Bitboard FileC = new(0x0404040404040404UL);
    public static readonly Bitboard FileD = new(0x0808080808080808UL);
    public static readonly Bitboard FileE = new(0x1010101010101010UL);
    public static readonly Bitboard FileF = new(0x2020202020202020UL);
    public static readonly Bitboard FileG = new(0x4040404040404040UL);
    public static readonly Bitboard FileH = new(0x8080808080808080UL);
    
    public static readonly Bitboard Rank1 = new(0x00000000000000FFUL);
    public static readonly Bitboard Rank2 = new(0x000000000000FF00UL);
    public static readonly Bitboard Rank3 = new(0x0000000000FF0000UL);
    public static readonly Bitboard Rank4 = new(0x00000000FF000000UL);
    public static readonly Bitboard Rank5 = new(0x000000FF00000000UL);
    public static readonly Bitboard Rank6 = new(0x0000FF0000000000UL);
    public static readonly Bitboard Rank7 = new(0x00FF000000000000UL);
    public static readonly Bitboard Rank8 = new(0xFF00000000000000UL);
    
    public static readonly Bitboard NotFileA = ~FileA;
    public static readonly Bitboard NotFileH = ~FileH;
    public static readonly Bitboard NotFileAB = ~(FileA | FileB);
    public static readonly Bitboard NotFileGH = ~(FileG | FileH);
    
    static AttackTables()
    {
        InitializePawnAttacks();
        InitializeKnightAttacks();
        InitializeKingAttacks();
        InitializeFilesRanksDiagonals();
        InitializeRayAttacks();
    }
    
    private static void InitializePawnAttacks()
    {
        for (var square = 0; square < 64; square++)
        {
            var sq = (Square)square;
            var bb = sq.ToBitboard();
            
            var whitePawnAttacks = Bitboard.Empty;
            if ((bb & NotFileA).IsNotEmpty()) whitePawnAttacks |= bb << 7;
            if ((bb & NotFileH).IsNotEmpty()) whitePawnAttacks |= bb << 9;
            s_pawnAttacks[square] = whitePawnAttacks;
            
            var blackPawnAttacks = Bitboard.Empty;
            if ((bb & NotFileA).IsNotEmpty()) blackPawnAttacks |= bb >> 9;
            if ((bb & NotFileH).IsNotEmpty()) blackPawnAttacks |= bb >> 7;
            s_pawnAttacks[square + 64] = blackPawnAttacks;
        }
    }
    
    private static void InitializeKnightAttacks()
    {
        var knightOffsets = new[] { -17, -15, -10, -6, 6, 10, 15, 17 };
        
        for (var square = 0; square < 64; square++)
        {
            var attacks = Bitboard.Empty;
            var file = square & 7;
            var rank = square >> 3;
            
            foreach (var offset in knightOffsets)
            {
                var targetSquare = square + offset;
                var targetFile = targetSquare & 7;
                var targetRank = targetSquare >> 3;
                
                if (targetSquare >= 0 && targetSquare < 64 &&
                    Math.Abs(file - targetFile) <= 2 &&
                    Math.Abs(rank - targetRank) <= 2 &&
                    Math.Abs(file - targetFile) + Math.Abs(rank - targetRank) == 3)
                {
                    attacks |= (Bitboard)(1UL << targetSquare);
                }
            }
            
            s_knightAttacks[square] = attacks;
        }
    }
    
    private static void InitializeKingAttacks()
    {
        var kingOffsets = new[] { -9, -8, -7, -1, 1, 7, 8, 9 };
        
        for (var square = 0; square < 64; square++)
        {
            var attacks = Bitboard.Empty;
            var file = square & 7;
            var rank = square >> 3;
            
            foreach (var offset in kingOffsets)
            {
                var targetSquare = square + offset;
                var targetFile = targetSquare & 7;
                var targetRank = targetSquare >> 3;
                
                if (targetSquare >= 0 && targetSquare < 64 &&
                    Math.Abs(file - targetFile) <= 1 &&
                    Math.Abs(rank - targetRank) <= 1)
                {
                    attacks |= (Bitboard)(1UL << targetSquare);
                }
            }
            
            s_kingAttacks[square] = attacks;
        }
    }
    
    private static void InitializeFilesRanksDiagonals()
    {
        for (var i = 0; i < 8; i++)
        {
            s_fileMasks[i] = FileA << i;
            s_rankMasks[i] = Rank1 << (i * 8);
        }
        
        for (var i = 0; i < 15; i++)
        {
            var diagonal = Bitboard.Empty;
            var antiDiagonal = Bitboard.Empty;
            
            for (var j = 0; j < 8; j++)
            {
                var diagFile = j;
                var diagRank = i - j;
                if (diagRank >= 0 && diagRank < 8)
                {
                    diagonal |= SquareExtensions.FromFileRank(diagFile, diagRank).ToBitboard();
                }
                
                var antiDiagFile = j;
                var antiDiagRank = 7 - i + j;
                if (antiDiagRank >= 0 && antiDiagRank < 8)
                {
                    antiDiagonal |= SquareExtensions.FromFileRank(antiDiagFile, antiDiagRank).ToBitboard();
                }
            }
            
            s_diagonalMasks[i] = diagonal;
            s_antiDiagonalMasks[i] = antiDiagonal;
        }
    }
    
    private static void InitializeRayAttacks()
    {
        var directions = new[] { 7, 8, 9, -1, 1, -9, -8, -7 };
        
        for (var square = 0; square < 64; square++)
        {
            for (var dir = 0; dir < 8; dir++)
            {
                var attacks = Bitboard.Empty;
                var offset = directions[dir];
                var currentSquare = square;
                
                while (true)
                {
                    var prevSquare = currentSquare;
                    currentSquare += offset;
                    
                    // Check for out of bounds
                    if (currentSquare < 0 || currentSquare >= 64)
                        break;
                    
                    var prevFile = prevSquare & 7;
                    var newFile = currentSquare & 7;
                    var prevRank = prevSquare >> 3;
                    var newRank = currentSquare >> 3;
                    
                    // Check for wrap-around by ensuring file and rank changes are at most 1
                    var fileDiff = Math.Abs(newFile - prevFile);
                    var rankDiff = Math.Abs(newRank - prevRank);
                    
                    // For diagonal moves, both should change by 1
                    // For straight moves, only one should change by 1
                    if (fileDiff > 1 || rankDiff > 1)
                        break;
                    
                    attacks |= (Bitboard)(1UL << currentSquare);
                }
                
                s_rayAttacks[square * 8 + dir] = attacks;
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard PawnAttacks(Square square, Color color) => 
        s_pawnAttacks[(int)square + ((int)color * 64)];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard KnightAttacks(Square square) => s_knightAttacks[(int)square];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard KingAttacks(Square square) => s_kingAttacks[(int)square];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard FileMask(int file) => s_fileMasks[file];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard RankMask(int rank) => s_rankMasks[rank];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard DiagonalMask(Square square) => 
        s_diagonalMasks[square.Rank() + square.File()];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard AntiDiagonalMask(Square square) => 
        s_antiDiagonalMasks[square.Rank() + 7 - square.File()];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard GetRay(Square square, int direction) => 
        s_rayAttacks[(int)square * 8 + direction];
    
    public static class Directions
    {
        public const int NorthWest = 0;
        public const int North = 1;
        public const int NorthEast = 2;
        public const int West = 3;
        public const int East = 4;
        public const int SouthWest = 5;
        public const int South = 6;
        public const int SouthEast = 7;
    }
}