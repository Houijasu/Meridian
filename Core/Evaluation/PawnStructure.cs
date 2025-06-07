namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;

/// <summary>
/// Evaluates pawn structure features including passed pawns, isolated pawns, doubled pawns, etc.
/// </summary>
public static class PawnStructure
{
    // Passed pawn bonuses by rank (from pawn's perspective)
    private static readonly int[] PassedPawnBonus = [0, 10, 20, 40, 70, 120, 200, 0];
    
    // Isolated pawn penalties by file
    private static readonly int[] IsolatedPawnPenalty = [30, 25, 25, 25, 25, 25, 25, 30];
    
    // Doubled pawn penalty
    private const int DoubledPawnPenalty = 20;
    
    // Backward pawn penalty
    private const int BackwardPawnPenalty = 15;
    
    // Connected pawn bonus by rank
    private static readonly int[] ConnectedPawnBonus = [0, 5, 10, 15, 25, 40, 60, 0];
    
    // File masks for pawn evaluation
    private static readonly ulong[] FileMask = new ulong[8];
    private static readonly ulong[] AdjacentFilesMask = new ulong[8];
    private static readonly ulong[] PassedPawnMask = new ulong[64];
    private static readonly ulong[] BackwardPawnMask = new ulong[64];
    
    static PawnStructure()
    {
        InitializeMasks();
    }
    
    private static void InitializeMasks()
    {
        // Initialize file masks
        for (int file = 0; file < 8; file++)
        {
            ulong mask = 0;
            for (int rank = 0; rank < 8; rank++)
            {
                mask |= 1UL << (rank * 8 + file);
            }
            FileMask[file] = mask;
            
            // Adjacent files mask
            ulong adjacent = 0;
            if (file > 0) adjacent |= FileMask[file - 1];
            if (file < 7) adjacent |= FileMask[file + 1];
            AdjacentFilesMask[file] = adjacent;
        }
        
        // Initialize passed pawn masks
        for (int sq = 0; sq < 64; sq++)
        {
            int file = sq & 7;
            int rank = sq >> 3;
            ulong mask = 0;
            
            // For white pawns, check squares in front on same and adjacent files
            for (int r = rank + 1; r < 8; r++)
            {
                mask |= 1UL << (r * 8 + file);
                if (file > 0) mask |= 1UL << (r * 8 + file - 1);
                if (file < 7) mask |= 1UL << (r * 8 + file + 1);
            }
            PassedPawnMask[sq] = mask;
            
            // Backward pawn mask (squares that could support this pawn)
            mask = 0;
            if (rank > 0)
            {
                if (file > 0) mask |= 1UL << ((rank - 1) * 8 + file - 1);
                if (file < 7) mask |= 1UL << ((rank - 1) * 8 + file + 1);
            }
            BackwardPawnMask[sq] = mask;
        }
    }
    
    /// <summary>
    /// Evaluates the pawn structure for both sides.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(in Position position)
    {
        int score = 0;
        
        // Evaluate white pawns
        score += EvaluatePawns(position.WhitePawns, position.BlackPawns, Color.White);
        
        // Evaluate black pawns
        score -= EvaluatePawns(position.BlackPawns, position.WhitePawns, Color.Black);
        
        return score;
    }
    
    /// <summary>
    /// Evaluates the pawn structure for both sides, returning separate middlegame and endgame scores.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EvaluateWithPhases(in Position position, out int mgScore, out int egScore, out ulong passedPawns)
    {
        mgScore = 0;
        egScore = 0;
        passedPawns = 0;
        
        // Evaluate white pawns
        EvaluatePawnsWithPhases(position.WhitePawns, position.BlackPawns, Color.White, 
                                ref mgScore, ref egScore, ref passedPawns);
        
        // Evaluate black pawns (subtract scores)
        int blackMg = 0, blackEg = 0;
        ulong blackPassed = 0;
        EvaluatePawnsWithPhases(position.BlackPawns, position.WhitePawns, Color.Black,
                                ref blackMg, ref blackEg, ref blackPassed);
        mgScore -= blackMg;
        egScore -= blackEg;
        
        // Flip black passed pawns to opponent's perspective
        while (blackPassed != 0)
        {
            int sq = Bitboard.PopLsb(ref blackPassed);
            passedPawns |= 1UL << (sq ^ 56);
        }
    }
    
    private static int EvaluatePawns(ulong friendlyPawns, ulong enemyPawns, Color color)
    {
        int score = 0;
        ulong pawns = friendlyPawns;
        
        while (pawns != 0)
        {
            int sq = Bitboard.PopLsb(ref pawns);
            int file = sq & 7;
            int rank = sq >> 3;
            
            // Adjust rank for black pawns
            if (color == Color.Black)
                rank = 7 - rank;
            
            // Passed pawn evaluation
            ulong passedMask = color == Color.White ? PassedPawnMask[sq] : PassedPawnMask[sq ^ 56];
            if ((passedMask & enemyPawns) == 0)
            {
                score += PassedPawnBonus[rank];
                
                // Additional bonus if pawn is supported
                if ((BackwardPawnMask[sq] & friendlyPawns) != 0)
                    score += PassedPawnBonus[rank] / 2;
            }
            
            // Isolated pawn evaluation
            if ((AdjacentFilesMask[file] & friendlyPawns) == 0)
            {
                score -= IsolatedPawnPenalty[file];
            }
            else
            {
                // Connected pawn bonus
                ulong supporters = BackwardPawnMask[sq] & friendlyPawns;
                if (supporters != 0)
                {
                    score += ConnectedPawnBonus[rank];
                }
                
                // Backward pawn evaluation
                if (rank is > 1 and < 6) // Not on starting or promotion ranks
                {
                    ulong stopSquare = color == Color.White ? 
                        1UL << (sq + 8) : 1UL << (sq - 8);
                        
                    if ((stopSquare & (friendlyPawns | enemyPawns)) == 0)
                    {
                        // Square in front is empty
                        ulong advancedFriendly = color == Color.White ?
                            friendlyPawns & ~((1UL << ((rank + 1) * 8)) - 1) :
                            friendlyPawns & ((1UL << (rank * 8)) - 1);
                            
                        if ((AdjacentFilesMask[file] & advancedFriendly) == 0)
                        {
                            score -= BackwardPawnPenalty;
                        }
                    }
                }
            }
        }
        
        // Doubled pawns evaluation
        for (int file = 0; file < 8; file++)
        {
            int pawnsOnFile = Bitboard.PopCount(friendlyPawns & FileMask[file]);
            if (pawnsOnFile > 1)
            {
                score -= DoubledPawnPenalty * (pawnsOnFile - 1);
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// Gets a bitboard of all passed pawns for the given color.
    /// </summary>
    public static ulong GetPassedPawns(in Position position, Color color)
    {
        ulong friendlyPawns = color == Color.White ? position.WhitePawns : position.BlackPawns;
        ulong enemyPawns = color == Color.White ? position.BlackPawns : position.WhitePawns;
        ulong passed = 0;
        ulong pawns = friendlyPawns;
        
        while (pawns != 0)
        {
            int sq = Bitboard.PopLsb(ref pawns);
            ulong passedMask = color == Color.White ? PassedPawnMask[sq] : PassedPawnMask[sq ^ 56];
            
            if ((passedMask & enemyPawns) == 0)
            {
                passed |= 1UL << sq;
            }
        }
        
        return passed;
    }
    
    private static void EvaluatePawnsWithPhases(ulong friendlyPawns, ulong enemyPawns, Color color,
                                                ref int mgScore, ref int egScore, ref ulong passedPawns)
    {
        ulong pawns = friendlyPawns;
        
        while (pawns != 0)
        {
            int sq = Bitboard.PopLsb(ref pawns);
            int file = sq & 7;
            int rank = sq >> 3;
            
            // Adjust rank for black pawns
            if (color == Color.Black)
                rank = 7 - rank;
            
            // Passed pawn evaluation
            ulong passedMask = color == Color.White ? PassedPawnMask[sq] : PassedPawnMask[sq ^ 56];
            if ((passedMask & enemyPawns) == 0)
            {
                // Passed pawns are more valuable in endgame
                mgScore += PassedPawnBonus[rank];
                egScore += PassedPawnBonus[rank] * 2;
                
                // Mark as passed pawn
                passedPawns |= 1UL << sq;
                
                // Additional bonus if pawn is supported
                if ((BackwardPawnMask[sq] & friendlyPawns) != 0)
                {
                    mgScore += PassedPawnBonus[rank] / 2;
                    egScore += PassedPawnBonus[rank];
                }
            }
            
            // Isolated pawn evaluation (worse in endgame)
            if ((AdjacentFilesMask[file] & friendlyPawns) == 0)
            {
                mgScore -= IsolatedPawnPenalty[file];
                egScore -= IsolatedPawnPenalty[file] * 2;
            }
            else
            {
                // Connected pawn evaluation
                ulong adjacent = AdjacentFilesMask[file] & friendlyPawns;
                if (adjacent != 0)
                {
                    // Check if pawn is supported by adjacent pawn on same or previous rank
                    ulong rankMask = 0xFFUL << (rank * 8);
                    ulong prevRankMask = rank > 0 ? 0xFFUL << ((rank - 1) * 8) : 0;
                    ulong nextRankMask = rank < 7 ? 0xFFUL << ((rank + 1) * 8) : 0;
                    
                    ulong supportMask = color == Color.White 
                        ? (adjacent & (rankMask | prevRankMask))
                        : (adjacent & (rankMask | nextRankMask));
                    
                    if (supportMask != 0)
                    {
                        mgScore += ConnectedPawnBonus[rank];
                        egScore += ConnectedPawnBonus[rank] / 2;
                    }
                }
            }
            
            // Doubled pawn evaluation
            ulong filePawns = FileMask[file] & friendlyPawns;
            if (Bitboard.PopCount(filePawns) > 1)
            {
                mgScore -= DoubledPawnPenalty;
                egScore -= DoubledPawnPenalty * 2;
            }
            
            // Backward pawn evaluation (less important in endgame)
            if (rank is > 1 and < 6)
            {
                ulong backwardMask = color == Color.White ? BackwardPawnMask[sq] : BackwardPawnMask[sq ^ 56];
                if ((backwardMask & friendlyPawns) == 0 && (AdjacentFilesMask[file] & friendlyPawns) != 0)
                {
                    mgScore -= BackwardPawnPenalty;
                    egScore -= BackwardPawnPenalty / 2;
                }
            }
        }
    }
}