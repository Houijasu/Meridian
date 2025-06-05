namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;

/// <summary>
/// Evaluates king safety features including pawn shield, open files, and attack patterns.
/// </summary>
public static class KingSafety
{
    // Penalties for missing pawns in front of king
    private const int MissingShieldPawnPenalty = 30;
    private const int WeakShieldPawnPenalty = 15;
    
    // Penalty for open/half-open files near king
    private const int OpenFileNearKingPenalty = 40;
    private const int HalfOpenFileNearKingPenalty = 20;
    
    // Bonus for castling rights
    private const int CastlingRightsBonus = 20;
    
    // Attack unit values (for calculating king danger)
    private const int KnightAttackUnit = 2;
    private const int BishopAttackUnit = 2;
    private const int RookAttackUnit = 3;
    private const int QueenAttackUnit = 5;
    
    // King danger thresholds
    private static readonly int[] KingDangerTable = new int[100];
    
    static KingSafety()
    {
        // Initialize king danger table (non-linear scaling)
        for (int i = 0; i < 100; i++)
        {
            KingDangerTable[i] = i * i / 10;
        }
    }
    
    /// <summary>
    /// Evaluates king safety for both sides.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(in Position position)
    {
        // Only evaluate king safety in middlegame
        if (position.WhiteQueens == 0 && position.BlackQueens == 0)
            return 0;
            
        int score = 0;
        
        // Evaluate white king safety
        score += EvaluateKingSafety(in position, Color.White);
        
        // Evaluate black king safety
        score -= EvaluateKingSafety(in position, Color.Black);
        
        return score;
    }
    
    private static int EvaluateKingSafety(in Position position, Color color)
    {
        ulong king = color == Color.White ? position.WhiteKing : position.BlackKing;
        if (king == 0) return 0;
        
        int kingSq = Bitboard.GetLsb(king);
        int kingFile = kingSq & 7;
        int kingRank = kingSq >> 3;
        
        int safety = 0;
        int attackUnits = 0;
        
        // Get friendly and enemy pieces
        ulong friendlyPawns = color == Color.White ? position.WhitePawns : position.BlackPawns;
        ulong enemyPawns = color == Color.White ? position.BlackPawns : position.WhitePawns;
        ulong allPawns = friendlyPawns | enemyPawns;
        
        // Evaluate pawn shield
        safety += EvaluatePawnShield(kingSq, friendlyPawns, color);
        
        // Check for open files near king
        for (int f = Math.Max(0, kingFile - 1); f <= Math.Min(7, kingFile + 1); f++)
        {
            ulong fileMask = 0;
            for (int r = 0; r < 8; r++)
            {
                fileMask |= 1UL << (r * 8 + f);
            }
            
            ulong pawnsOnFile = allPawns & fileMask;
            if (pawnsOnFile == 0)
            {
                safety -= OpenFileNearKingPenalty;
            }
            else if ((friendlyPawns & fileMask) == 0)
            {
                safety -= HalfOpenFileNearKingPenalty;
            }
        }
        
        // Bonus for castling rights
        if (color == Color.White)
        {
            if ((position.CastlingRights & CastlingRights.WhiteKingside) != 0 ||
                (position.CastlingRights & CastlingRights.WhiteQueenside) != 0)
            {
                safety += CastlingRightsBonus;
            }
        }
        else
        {
            if ((position.CastlingRights & CastlingRights.BlackKingside) != 0 ||
                (position.CastlingRights & CastlingRights.BlackQueenside) != 0)
            {
                safety += CastlingRightsBonus;
            }
        }
        
        // Count enemy pieces attacking king zone
        ulong kingZone = GetKingZone(kingSq);
        
        if (color == Color.White)
        {
            // Count black pieces attacking white king zone
            attackUnits += CountAttackingPieces(kingZone, position.BlackKnights, KnightAttackUnit);
            attackUnits += CountAttackingPieces(kingZone, position.BlackBishops, BishopAttackUnit);
            attackUnits += CountAttackingPieces(kingZone, position.BlackRooks, RookAttackUnit);
            attackUnits += CountAttackingPieces(kingZone, position.BlackQueens, QueenAttackUnit);
        }
        else
        {
            // Count white pieces attacking black king zone
            attackUnits += CountAttackingPieces(kingZone, position.WhiteKnights, KnightAttackUnit);
            attackUnits += CountAttackingPieces(kingZone, position.WhiteBishops, BishopAttackUnit);
            attackUnits += CountAttackingPieces(kingZone, position.WhiteRooks, RookAttackUnit);
            attackUnits += CountAttackingPieces(kingZone, position.WhiteQueens, QueenAttackUnit);
        }
        
        // Apply king danger penalty
        if (attackUnits > 0)
        {
            safety -= KingDangerTable[Math.Min(attackUnits, 99)];
        }
        
        return safety;
    }
    
    private static int EvaluatePawnShield(int kingSq, ulong friendlyPawns, Color color)
    {
        int score = 0;
        int kingFile = kingSq & 7;
        int kingRank = kingSq >> 3;
        
        // Check castled positions
        bool isKingsideCastled = (color == Color.White && kingFile >= 6) || 
                                 (color == Color.Black && kingFile >= 6);
        bool isQueensideCastled = (color == Color.White && kingFile <= 2) || 
                                  (color == Color.Black && kingFile <= 2);
        
        if (isKingsideCastled || isQueensideCastled)
        {
            // Check pawn shield for castled king
            int startFile = isKingsideCastled ? 5 : 0;
            int endFile = isKingsideCastled ? 7 : 2;
            
            for (int f = startFile; f <= endFile; f++)
            {
                int shieldRank = color == Color.White ? kingRank + 1 : kingRank - 1;
                
                if (shieldRank >= 0 && shieldRank <= 7)
                {
                    int shieldSq = shieldRank * 8 + f;
                    if ((friendlyPawns & (1UL << shieldSq)) == 0)
                    {
                        // Missing shield pawn
                        score -= MissingShieldPawnPenalty;
                        
                        // Check if pawn is advanced
                        int advancedRank = color == Color.White ? shieldRank + 1 : shieldRank - 1;
                        if (advancedRank >= 0 && advancedRank <= 7)
                        {
                            int advancedSq = advancedRank * 8 + f;
                            if ((friendlyPawns & (1UL << advancedSq)) != 0)
                            {
                                // Pawn is advanced (weaker shield)
                                score += MissingShieldPawnPenalty - WeakShieldPawnPenalty;
                            }
                        }
                    }
                }
            }
        }
        
        return score;
    }
    
    private static ulong GetKingZone(int kingSq)
    {
        ulong zone = 0;
        int file = kingSq & 7;
        int rank = kingSq >> 3;
        
        // Include all squares within 2 squares of the king
        for (int r = Math.Max(0, rank - 2); r <= Math.Min(7, rank + 2); r++)
        {
            for (int f = Math.Max(0, file - 2); f <= Math.Min(7, file + 2); f++)
            {
                zone |= 1UL << (r * 8 + f);
            }
        }
        
        return zone;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountAttackingPieces(ulong kingZone, ulong pieces, int unitValue)
    {
        // Simplified: count pieces near king zone
        // In a full implementation, we would check actual attack patterns
        int count = 0;
        while (pieces != 0)
        {
            int sq = Bitboard.PopLsb(ref pieces);
            int file = sq & 7;
            int rank = sq >> 3;
            
            // Check if piece is close to king zone
            bool nearZone = false;
            for (int r = Math.Max(0, rank - 2); r <= Math.Min(7, rank + 2); r++)
            {
                for (int f = Math.Max(0, file - 2); f <= Math.Min(7, file + 2); f++)
                {
                    if ((kingZone & (1UL << (r * 8 + f))) != 0)
                    {
                        nearZone = true;
                        break;
                    }
                }
                if (nearZone) break;
            }
            
            if (nearZone) count++;
        }
        
        return count * unitValue;
    }
}