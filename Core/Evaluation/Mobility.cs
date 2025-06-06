namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;

/// <summary>
/// Evaluates piece mobility - the number of squares each piece can move to.
/// </summary>
public static class Mobility
{
    // Mobility bonuses per piece type (per square of mobility)
    private const int KnightMobilityBonus = 4;
    private const int BishopMobilityBonus = 3;
    private const int RookMobilityBonus = 2;
    private const int QueenMobilityBonus = 1;
    
    // Penalties for trapped pieces
    private const int TrappedKnightPenalty = 50;
    private const int TrappedBishopPenalty = 50;
    private const int TrappedRookPenalty = 100;
    
    // Center squares mask (e4, d4, e5, d5)
    private const ulong CenterMask = 0x0000001818000000UL;
    
    // Extended center mask (c3-f3, c4-f4, c5-f5, c6-f6)
    private const ulong ExtendedCenterMask = 0x00003C3C3C3C0000UL;
    
    /// <summary>
    /// Evaluates mobility for both sides.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(in Position position)
    {
        int score = 0;
        
        // Get occupied squares
        ulong occupied = position.WhitePieces | position.BlackPieces;
        
        // Evaluate white pieces
        score += EvaluateSideMobility(in position, Color.White, occupied);
        
        // Evaluate black pieces
        score -= EvaluateSideMobility(in position, Color.Black, occupied);
        
        return score;
    }
    
    private static int EvaluateSideMobility(in Position position, Color color, ulong occupied)
    {
        int score = 0;
        
        // Get friendly and enemy pieces
        ulong friendlyPieces = color == Color.White ? position.WhitePieces : position.BlackPieces;
        ulong enemyPieces = color == Color.White ? position.BlackPieces : position.WhitePieces;
        
        // Evaluate knight mobility
        ulong knights = color == Color.White ? position.WhiteKnights : position.BlackKnights;
        while (knights != 0)
        {
            int sq = Bitboard.PopLsb(ref knights);
            ulong moves = GetKnightMoves(sq) & ~friendlyPieces;
            int mobility = Bitboard.PopCount(moves);
            
            // Base mobility score
            score += mobility * KnightMobilityBonus;
            
            // Penalty for trapped knight (less than 3 moves)
            if (mobility < 3)
            {
                score -= TrappedKnightPenalty * (3 - mobility) / 3;
            }
            
            // Bonus for knight on outpost (protected by pawn, can't be attacked by enemy pawns)
            if (IsKnightOnOutpost(sq, color, position))
            {
                score += 20;
            }
        }
        
        // Evaluate bishop mobility
        ulong bishops = color == Color.White ? position.WhiteBishops : position.BlackBishops;
        while (bishops != 0)
        {
            int sq = Bitboard.PopLsb(ref bishops);
            ulong moves = GetBishopMoves(sq, occupied) & ~friendlyPieces;
            int mobility = Bitboard.PopCount(moves);
            
            // Base mobility score
            score += mobility * BishopMobilityBonus;
            
            // Penalty for trapped bishop
            if (mobility < 3)
            {
                score -= TrappedBishopPenalty * (3 - mobility) / 3;
            }
            
            // Bonus for bishop pair
            if (Bitboard.PopCount(color == Color.White ? position.WhiteBishops : position.BlackBishops) >= 2)
            {
                score += 30;
            }
        }
        
        // Evaluate rook mobility
        ulong rooks = color == Color.White ? position.WhiteRooks : position.BlackRooks;
        while (rooks != 0)
        {
            int sq = Bitboard.PopLsb(ref rooks);
            ulong moves = GetRookMoves(sq, occupied) & ~friendlyPieces;
            int mobility = Bitboard.PopCount(moves);
            
            // Base mobility score
            score += mobility * RookMobilityBonus;
            
            // Penalty for trapped rook
            if (mobility < 4)
            {
                score -= TrappedRookPenalty * (4 - mobility) / 4;
            }
            
            // Bonus for rook on open file
            int file = sq & 7;
            ulong fileMask = GetFileMask(file);
            if ((fileMask & (position.WhitePawns | position.BlackPawns)) == 0)
            {
                score += 25; // Open file
            }
            else if ((fileMask & (color == Color.White ? position.WhitePawns : position.BlackPawns)) == 0)
            {
                score += 15; // Half-open file
            }
            
            // Bonus for rook on 7th rank
            int rank = sq >> 3;
            if ((color == Color.White && rank == 6) || (color == Color.Black && rank == 1))
            {
                score += 20;
            }
        }
        
        // Evaluate queen mobility
        ulong queens = color == Color.White ? position.WhiteQueens : position.BlackQueens;
        while (queens != 0)
        {
            int sq = Bitboard.PopLsb(ref queens);
            ulong moves = (GetBishopMoves(sq, occupied) | GetRookMoves(sq, occupied)) & ~friendlyPieces;
            int mobility = Bitboard.PopCount(moves);
            
            // Base mobility score (lower weight for queen)
            score += mobility * QueenMobilityBonus;
            
            // Penalty for early queen development
            if (position.HalfmoveClock < 10)
            {
                int homeRank = color == Color.White ? 0 : 7;
                if ((sq >> 3) != homeRank)
                {
                    score -= 15;
                }
            }
        }
        
        return score;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetKnightMoves(int sq)
    {
        // Simplified knight move generation
        int rank = sq >> 3;
        int file = sq & 7;
        ulong moves = 0;
        
        int[] rankOffsets = { -2, -2, -1, -1, 1, 1, 2, 2 };
        int[] fileOffsets = { -1, 1, -2, 2, -2, 2, -1, 1 };
        
        for (int i = 0; i < 8; i++)
        {
            int newRank = rank + rankOffsets[i];
            int newFile = file + fileOffsets[i];
            
            if (newRank is >= 0 and <= 7 && newFile is >= 0 and <= 7)
            {
                moves |= 1UL << (newRank * 8 + newFile);
            }
        }
        
        return moves;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetBishopMoves(int sq, ulong occupied)
    {
        // Simplified bishop move generation (would use magic bitboards in practice)
        ulong moves = 0;
        int rank = sq >> 3;
        int file = sq & 7;
        
        // Northeast
        for (int r = rank + 1, f = file + 1; r <= 7 && f <= 7; r++, f++)
        {
            moves |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }
        
        // Northwest
        for (int r = rank + 1, f = file - 1; r <= 7 && f >= 0; r++, f--)
        {
            moves |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }
        
        // Southeast
        for (int r = rank - 1, f = file + 1; r >= 0 && f <= 7; r--, f++)
        {
            moves |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }
        
        // Southwest
        for (int r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
        {
            moves |= 1UL << (r * 8 + f);
            if ((occupied & (1UL << (r * 8 + f))) != 0) break;
        }
        
        return moves;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetRookMoves(int sq, ulong occupied)
    {
        // Simplified rook move generation (would use magic bitboards in practice)
        ulong moves = 0;
        int rank = sq >> 3;
        int file = sq & 7;
        
        // North
        for (int r = rank + 1; r <= 7; r++)
        {
            moves |= 1UL << (r * 8 + file);
            if ((occupied & (1UL << (r * 8 + file))) != 0) break;
        }
        
        // South
        for (int r = rank - 1; r >= 0; r--)
        {
            moves |= 1UL << (r * 8 + file);
            if ((occupied & (1UL << (r * 8 + file))) != 0) break;
        }
        
        // East
        for (int f = file + 1; f <= 7; f++)
        {
            moves |= 1UL << (rank * 8 + f);
            if ((occupied & (1UL << (rank * 8 + f))) != 0) break;
        }
        
        // West
        for (int f = file - 1; f >= 0; f--)
        {
            moves |= 1UL << (rank * 8 + f);
            if ((occupied & (1UL << (rank * 8 + f))) != 0) break;
        }
        
        return moves;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetFileMask(int file)
    {
        ulong mask = 0;
        for (int rank = 0; rank < 8; rank++)
        {
            mask |= 1UL << (rank * 8 + file);
        }
        return mask;
    }
    
    private static bool IsKnightOnOutpost(int sq, Color color, in Position position)
    {
        int rank = sq >> 3;
        int file = sq & 7;
        
        // Knight should be on ranks 4-6 for white, 2-4 for black
        if (color == Color.White)
        {
            if (rank < 3 || rank > 5) return false;
        }
        else
        {
            if (rank < 2 || rank > 4) return false;
        }
        
        // Check if protected by friendly pawn
        ulong friendlyPawns = color == Color.White ? position.WhitePawns : position.BlackPawns;
        int pawnRank = color == Color.White ? rank - 1 : rank + 1;
        
        // Given the rank constraints above, pawnRank is always valid (0-7)
        bool protectedByPawn = false;
        
        if (file > 0 && (friendlyPawns & (1UL << (pawnRank * 8 + file - 1))) != 0)
            protectedByPawn = true;
        if (file < 7 && (friendlyPawns & (1UL << (pawnRank * 8 + file + 1))) != 0)
            protectedByPawn = true;
            
        if (!protectedByPawn) return false;
        
        // Check if can't be attacked by enemy pawns
        ulong enemyPawns = color == Color.White ? position.BlackPawns : position.WhitePawns;
        
        // Check all ranks ahead
        if (color == Color.White)
        {
            for (int r = rank + 1; r <= 6; r++)
            {
                if (file > 0 && (enemyPawns & (1UL << (r * 8 + file - 1))) != 0)
                    return false;
                if (file < 7 && (enemyPawns & (1UL << (r * 8 + file + 1))) != 0)
                    return false;
            }
        }
        else
        {
            for (int r = rank - 1; r >= 1; r--)
            {
                if (file > 0 && (enemyPawns & (1UL << (r * 8 + file - 1))) != 0)
                    return false;
                if (file < 7 && (enemyPawns & (1UL << (r * 8 + file + 1))) != 0)
                    return false;
            }
        }
        
        return true;
    }
}