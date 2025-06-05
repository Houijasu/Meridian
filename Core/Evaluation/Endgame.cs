namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;

/// <summary>
/// Specialized evaluation for endgame positions.
/// </summary>
public static class Endgame
{
    // King activity bonuses in endgame
    private const int KingCentralizationBonus = 20;
    private const int KingActivityBonus = 5;
    
    // Passed pawn race bonus
    private const int PassedPawnRaceBonus = 50;
    
    // Rook behind passed pawn bonus
    private const int RookBehindPassedPawnBonus = 30;
    
    // Distance tables for king proximity calculations
    private static readonly int[,] Distance = new int[64, 64];
    
    static Endgame()
    {
        InitializeDistanceTables();
    }
    
    private static void InitializeDistanceTables()
    {
        for (int sq1 = 0; sq1 < 64; sq1++)
        {
            for (int sq2 = 0; sq2 < 64; sq2++)
            {
                int file1 = sq1 & 7, rank1 = sq1 >> 3;
                int file2 = sq2 & 7, rank2 = sq2 >> 3;
                Distance[sq1, sq2] = Math.Max(Math.Abs(file1 - file2), Math.Abs(rank1 - rank2));
            }
        }
    }
    
    /// <summary>
    /// Evaluates endgame-specific features.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(in Position position)
    {
        // Only apply endgame evaluation when material is low
        if (!IsEndgame(in position))
            return 0;
            
        int score = 0;
        
        // Evaluate king activity
        score += EvaluateKingActivity(in position);
        
        // Evaluate passed pawns in endgame
        score += EvaluateEndgamePassedPawns(in position);
        
        // Evaluate specific endgames
        score += EvaluateSpecificEndgames(in position);
        
        return score;
    }
    
    /// <summary>
    /// Determines if the position is an endgame based on material.
    /// </summary>
    public static bool IsEndgame(in Position position)
    {
        // No queens = endgame
        if (position.WhiteQueens == 0 && position.BlackQueens == 0)
            return true;
            
        // Queen + minor piece or less per side = endgame
        int whiteMajors = Bitboard.PopCount(position.WhiteRooks) + Bitboard.PopCount(position.WhiteQueens);
        int blackMajors = Bitboard.PopCount(position.BlackRooks) + Bitboard.PopCount(position.BlackQueens);
        int whiteMinors = Bitboard.PopCount(position.WhiteKnights | position.WhiteBishops);
        int blackMinors = Bitboard.PopCount(position.BlackKnights | position.BlackBishops);
        
        return whiteMajors + whiteMinors <= 3 && blackMajors + blackMinors <= 3;
    }
    
    private static int EvaluateKingActivity(in Position position)
    {
        int score = 0;
        
        // White king
        if (position.WhiteKing != 0)
        {
            int wKingSq = Bitboard.GetLsb(position.WhiteKing);
            score += EvaluateKingPosition(wKingSq, Color.White);
        }
        
        // Black king
        if (position.BlackKing != 0)
        {
            int bKingSq = Bitboard.GetLsb(position.BlackKing);
            score -= EvaluateKingPosition(bKingSq, Color.Black);
        }
        
        return score;
    }
    
    private static int EvaluateKingPosition(int kingSq, Color color)
    {
        int score = 0;
        int file = kingSq & 7;
        int rank = kingSq >> 3;
        
        // Centralization bonus
        int centerDistance = Math.Max(Math.Abs(file - 3), Math.Abs(file - 4)) + 
                           Math.Max(Math.Abs(rank - 3), Math.Abs(rank - 4));
        score += KingCentralizationBonus * (6 - centerDistance) / 6;
        
        // Activity bonus (distance from starting position)
        int startRank = color == Color.White ? 0 : 7;
        int advancement = color == Color.White ? rank : 7 - rank;
        score += KingActivityBonus * advancement;
        
        return score;
    }
    
    private static int EvaluateEndgamePassedPawns(in Position position)
    {
        int score = 0;
        
        // Get passed pawns for both sides
        ulong whitePassedPawns = PawnStructure.GetPassedPawns(in position, Color.White);
        ulong blackPassedPawns = PawnStructure.GetPassedPawns(in position, Color.Black);
        
        // Evaluate white passed pawns
        while (whitePassedPawns != 0)
        {
            int sq = Bitboard.PopLsb(ref whitePassedPawns);
            score += EvaluatePassedPawnEndgame(sq, Color.White, in position);
        }
        
        // Evaluate black passed pawns
        while (blackPassedPawns != 0)
        {
            int sq = Bitboard.PopLsb(ref blackPassedPawns);
            score -= EvaluatePassedPawnEndgame(sq, Color.Black, in position);
        }
        
        return score;
    }
    
    private static int EvaluatePassedPawnEndgame(int pawnSq, Color pawnColor, in Position position)
    {
        int score = 0;
        int rank = pawnSq >> 3;
        int file = pawnSq & 7;
        
        // Adjust rank for black pawns
        if (pawnColor == Color.Black)
            rank = 7 - rank;
            
        // Rule of the square
        int promotionSq = pawnColor == Color.White ? 56 + file : file;
        int enemyKingSq = Bitboard.GetLsb(pawnColor == Color.White ? position.BlackKing : position.WhiteKing);
        int friendlyKingSq = Bitboard.GetLsb(pawnColor == Color.White ? position.WhiteKing : position.BlackKing);
        
        // Safety check - kings must exist
        if (enemyKingSq < 0 || enemyKingSq >= 64 || friendlyKingSq < 0 || friendlyKingSq >= 64)
            return score;
        
        int distanceToPromotion = 7 - rank;
        int enemyKingDistance = Distance[enemyKingSq, promotionSq];
        
        // Check if enemy king can catch the pawn
        bool canBeCaught = enemyKingDistance <= distanceToPromotion;
        if (position.SideToMove != pawnColor)
            canBeCaught = enemyKingDistance <= distanceToPromotion + 1;
            
        if (!canBeCaught)
        {
            score += PassedPawnRaceBonus + distanceToPromotion * 20;
        }
        
        // King proximity bonus
        score += 10 * (7 - Distance[friendlyKingSq, pawnSq]);
        score -= 10 * (7 - Distance[enemyKingSq, pawnSq]);
        
        // Rook behind passed pawn
        ulong rooks = pawnColor == Color.White ? position.WhiteRooks : position.BlackRooks;
        ulong fileMask = GetFileMask(file);
        
        if ((rooks & fileMask) != 0)
        {
            int rookSq = Bitboard.GetLsb(rooks & fileMask);
            int rookRank = rookSq >> 3;
            
            // Rook behind pawn
            if ((pawnColor == Color.White && rookRank < rank) || 
                (pawnColor == Color.Black && rookRank > rank))
            {
                score += RookBehindPassedPawnBonus;
            }
        }
        
        return score;
    }
    
    private static int EvaluateSpecificEndgames(in Position position)
    {
        int score = 0;
        
        // KRP vs KR endgame
        if (Bitboard.PopCount(position.WhitePawns) == 1 && position.BlackPawns == 0 &&
            Bitboard.PopCount(position.WhiteRooks) == 1 && Bitboard.PopCount(position.BlackRooks) == 1 &&
            position.WhiteQueens == 0 && position.BlackQueens == 0 &&
            position.WhiteKnights == 0 && position.BlackKnights == 0 &&
            position.WhiteBishops == 0 && position.BlackBishops == 0)
        {
            score += EvaluateKRPvsKR(in position);
        }
        // KRP vs KR endgame (reversed)
        else if (position.WhitePawns == 0 && Bitboard.PopCount(position.BlackPawns) == 1 &&
                 Bitboard.PopCount(position.WhiteRooks) == 1 && Bitboard.PopCount(position.BlackRooks) == 1 &&
                 position.WhiteQueens == 0 && position.BlackQueens == 0 &&
                 position.WhiteKnights == 0 && position.BlackKnights == 0 &&
                 position.WhiteBishops == 0 && position.BlackBishops == 0)
        {
            score -= EvaluateKRPvsKR(in position);
        }
        
        // KBN vs K endgame
        if (position.WhitePawns == 0 && position.BlackPawns == 0 &&
            position.WhiteRooks == 0 && position.BlackRooks == 0 &&
            position.WhiteQueens == 0 && position.BlackQueens == 0)
        {
            if (Bitboard.PopCount(position.WhiteKnights) == 1 && 
                Bitboard.PopCount(position.WhiteBishops) == 1 &&
                position.BlackKnights == 0 && position.BlackBishops == 0)
            {
                score += EvaluateKBNvsK(in position, Color.White);
            }
            else if (Bitboard.PopCount(position.BlackKnights) == 1 && 
                     Bitboard.PopCount(position.BlackBishops) == 1 &&
                     position.WhiteKnights == 0 && position.WhiteBishops == 0)
            {
                score -= EvaluateKBNvsK(in position, Color.Black);
            }
        }
        
        return score;
    }
    
    private static int EvaluateKRPvsKR(in Position position)
    {
        // Simplified evaluation for KRP vs KR
        // In practice, this would use tablebase knowledge
        int pawnSq = Bitboard.GetLsb(position.WhitePawns);
        int rank = pawnSq >> 3;
        
        // More advanced pawns are better
        return rank * 20;
    }
    
    private static int EvaluateKBNvsK(in Position position, Color winningColor)
    {
        // KBN vs K is a theoretical win, but difficult
        // Drive the defending king to the corner of the bishop's color
        int defendingKingSq = Bitboard.GetLsb(winningColor == Color.White ? position.BlackKing : position.WhiteKing);
        int bishopSq = Bitboard.GetLsb(winningColor == Color.White ? position.WhiteBishops : position.BlackBishops);
        
        // Determine bishop color
        bool bishopOnLightSquare = ((bishopSq >> 3) + (bishopSq & 7)) % 2 == 1;
        
        // Light squared corners: a1 (0) and h8 (63)
        // Dark squared corners: a8 (56) and h1 (7)
        int targetCorner1, targetCorner2;
        if (bishopOnLightSquare)
        {
            targetCorner1 = 0;  // a1
            targetCorner2 = 63; // h8
        }
        else
        {
            targetCorner1 = 56; // a8
            targetCorner2 = 7;  // h1
        }
        
        // Score based on distance to correct corner
        int minDistance = Math.Min(Distance[defendingKingSq, targetCorner1], 
                                  Distance[defendingKingSq, targetCorner2]);
        
        return 200 - minDistance * 30;
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
    
    /// <summary>
    /// Calculates the endgame phase factor (0 = opening/middlegame, 256 = pure endgame).
    /// </summary>
    public static int GetEndgamePhase(in Position position)
    {
        // Count material (excluding pawns and kings)
        int material = 0;
        material += Bitboard.PopCount(position.WhiteQueens) * 9;
        material += Bitboard.PopCount(position.BlackQueens) * 9;
        material += Bitboard.PopCount(position.WhiteRooks) * 5;
        material += Bitboard.PopCount(position.BlackRooks) * 5;
        material += Bitboard.PopCount(position.WhiteBishops) * 3;
        material += Bitboard.PopCount(position.BlackBishops) * 3;
        material += Bitboard.PopCount(position.WhiteKnights) * 3;
        material += Bitboard.PopCount(position.BlackKnights) * 3;
        
        // Max material = 2*(9+2*5+2*3+2*3) = 62
        const int maxMaterial = 62;
        
        // Calculate phase (0-256)
        if (material >= maxMaterial)
            return 0; // Opening
            
        return 256 * (maxMaterial - material) / maxMaterial;
    }
}