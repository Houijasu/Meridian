namespace Meridian.Core.Search;

using System.Runtime.CompilerServices;

/// <summary>
/// Handles move ordering for alpha-beta search.
/// Better move ordering leads to more beta cutoffs and faster search.
/// </summary>
public sealed class MoveOrdering
{
    private static readonly int[] MVVLVATable = new int[12 * 12];
    private readonly Move[,] killerMoves = new Move[SearchConstants.MaxPly, 2];
    private readonly int[,] historyTable = new int[64, 64]; // Butterfly table [from][to]
    private readonly Move[,] counterMoves = new Move[12, 64];
    
    static MoveOrdering()
    {
        InitializeMVVLVA();
    }
    
    private static void InitializeMVVLVA()
    {
        const int PawnValue = 100;
        const int KnightValue = 320;
        const int BishopValue = 330;
        const int RookValue = 500;
        const int QueenValue = 900;
        const int KingValue = 10000;
        
        int[] victimValues = [
            PawnValue, KnightValue, BishopValue, RookValue, QueenValue, KingValue,
                              PawnValue, KnightValue, BishopValue, RookValue, QueenValue, KingValue
        ];
        
        int[] attackerValues = [
            PawnValue, KnightValue, BishopValue, RookValue, QueenValue, KingValue,
                                PawnValue, KnightValue, BishopValue, RookValue, QueenValue, KingValue
        ];
        
        for (int victim = 0; victim < 12; victim++)
        {
            for (int attacker = 0; attacker < 12; attacker++)
            {
                MVVLVATable[victim * 12 + attacker] = victimValues[victim] * 10 - attackerValues[attacker];
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PieceToIndex(Piece piece) => piece switch
    {
        Piece.WhitePawn => 0,
        Piece.WhiteKnight => 1,
        Piece.WhiteBishop => 2,
        Piece.WhiteRook => 3,
        Piece.WhiteQueen => 4,
        Piece.WhiteKing => 5,
        Piece.BlackPawn => 6,
        Piece.BlackKnight => 7,
        Piece.BlackBishop => 8,
        Piece.BlackRook => 9,
        Piece.BlackQueen => 10,
        Piece.BlackKing => 11,
        _ => -1
    };
    
    /// <summary>
    /// Scores moves for ordering. Higher scores are searched first.
    /// </summary>
    public void ScoreMoves(ReadOnlySpan<Move> moves, Span<ScoredMove> scoredMoves, int moveCount, Move ttMove, int ply, in Position position)
    {
        for (int i = 0; i < moveCount; i++)
        {
            var move = moves[i];
            int score;
            
            if (move.Equals(ttMove))
            {
                score = 1000000;
            }
            else if (move.IsCapture)
            {
                // Use SEE for capture ordering
                var seeValue = StaticExchangeEvaluation.Evaluate(in position, move);
                if (seeValue >= 0)
                {
                    // Good captures ordered by SEE value and MVV/LVA as tiebreaker
                    score = 900000 + seeValue * 1000 + GetMVVLVAScore(move);
                }
                else
                {
                    // Bad captures (losing exchanges) scored low
                    score = -100000 + seeValue;
                }
            }
            else if (move.IsPromotion)
            {
                score = 800000 + (int)move.GetPromotionType() * 100;
            }
            else if (move.Equals(killerMoves[ply, 0]))
            {
                score = 700000; // First killer from current ply
            }
            else if (move.Equals(killerMoves[ply, 1]))
            {
                score = 690000; // Second killer from current ply
            }
            else if (ply >= 2 && (move.Equals(killerMoves[ply - 2, 0]) || move.Equals(killerMoves[ply - 2, 1])))
            {
                score = 680000; // Killer from 2 plies ago
            }
            else
            {
                // Default to history score
                score = GetHistoryScore(move);
                
                // Check counter move (if we have a previous move from killers)
                if (ply > 0)
                {
                    // Use the first killer from previous ply as an approximation of last move
                    var prevMove = killerMoves[ply - 1, 0];
                    if (!prevMove.IsNull)
                    {
                        var counterMove = GetCounterMove(prevMove);
                        if (move.Equals(counterMove))
                        {
                            score = 670000; // Counter move
                        }
                    }
                }
            }
            
            scoredMoves[i] = new ScoredMove(move, score);
        }
    }
    
    /// <summary>
    /// Sorts moves by their scores using insertion sort.
    /// Insertion sort is efficient for nearly sorted data and small arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SortMoves(Span<ScoredMove> moves, int moveCount)
    {
        for (int i = 1; i < moveCount; i++)
        {
            var move = moves[i];
            int j = i - 1;
            
            while (j >= 0 && moves[j].Score < move.Score)
            {
                moves[j + 1] = moves[j];
                j--;
            }
            
            moves[j + 1] = move;
        }
    }
    
    /// <summary>
    /// Picks the best move from the remaining moves using partial selection sort.
    /// This is more efficient than sorting all moves when we might get a cutoff early.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PickBestMove(Span<ScoredMove> moves, int currentIndex, int moveCount)
    {
        int bestIndex = currentIndex;
        int bestScore = moves[currentIndex].Score;
        
        for (int i = currentIndex + 1; i < moveCount; i++)
        {
            if (moves[i].Score > bestScore)
            {
                bestScore = moves[i].Score;
                bestIndex = i;
            }
        }
        
        if (bestIndex != currentIndex)
        {
            (moves[currentIndex], moves[bestIndex]) = (moves[bestIndex], moves[currentIndex]);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetMVVLVAScore(Move move)
    {
        int victimIndex = PieceToIndex(move.CapturedPiece);
        int attackerIndex = PieceToIndex(move.Piece);
        
        if (victimIndex < 0 || attackerIndex < 0) return 0;
        
        return MVVLVATable[victimIndex * 12 + attackerIndex];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsKillerMove(Move move, int ply)
    {
        // Check killers from current ply
        if (move.Equals(killerMoves[ply, 0]) || move.Equals(killerMoves[ply, 1]))
            return true;
            
        // Also check killers from 2 plies ago (same side to move)
        if (ply >= 2)
        {
            return move.Equals(killerMoves[ply - 2, 0]) || move.Equals(killerMoves[ply - 2, 1]);
        }
        
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetHistoryScore(Move move)
    {
        return historyTable[(int)move.From, (int)move.To];
    }
    
    /// <summary>
    /// Updates killer moves for a given ply.
    /// </summary>
    public void UpdateKillers(Move move, int ply)
    {
        if (!move.IsCapture && !move.Equals(killerMoves[ply, 0]))
        {
            killerMoves[ply, 1] = killerMoves[ply, 0];
            killerMoves[ply, 0] = move;
        }
    }
    
    /// <summary>
    /// Updates history heuristic for a move that caused a beta cutoff.
    /// </summary>
    public void UpdateHistory(Move move, int depth)
    {
        if (!move.IsCapture)
        {
            historyTable[(int)move.From, (int)move.To] += depth * depth;
            
            if (historyTable[(int)move.From, (int)move.To] > 100000)
            {
                AgeHistoryTable();
            }
        }
    }
    
    /// <summary>
    /// Updates history malus for moves that failed to cause a cutoff.
    /// </summary>
    public void UpdateHistoryMalus(ReadOnlySpan<Move> quietMoves, int quietCount, int depth)
    {
        for (int i = 0; i < quietCount; i++)
        {
            var move = quietMoves[i];
            if (!move.IsCapture)
            {
                historyTable[(int)move.From, (int)move.To] -= depth * depth;
                
                // Ensure history scores don't go too negative
                if (historyTable[(int)move.From, (int)move.To] < -100000)
                {
                    AgeHistoryTable();
                }
            }
        }
    }
    
    /// <summary>
    /// Updates countermove heuristic.
    /// </summary>
    public void UpdateCounterMove(Move previousMove, Move move)
    {
        if (!previousMove.IsNull && !move.IsCapture)
        {
            int pieceIndex = PieceToIndex(previousMove.Piece);
            if (pieceIndex >= 0)
            {
                counterMoves[pieceIndex, (int)previousMove.To] = move;
            }
        }
    }
    
    /// <summary>
    /// Gets the countermove for a given previous move.
    /// </summary>
    public Move GetCounterMove(Move previousMove)
    {
        if (previousMove.IsNull) return Move.Null;
        
        int pieceIndex = PieceToIndex(previousMove.Piece);
        if (pieceIndex < 0) return Move.Null;
        
        return counterMoves[pieceIndex, (int)previousMove.To];
    }
    
    /// <summary>
    /// Ages the history table to prevent overflow and maintain relevance.
    /// </summary>
    private void AgeHistoryTable()
    {
        for (int i = 0; i < 64; i++)
        {
            for (int j = 0; j < 64; j++)
            {
                historyTable[i, j] /= 2;
            }
        }
    }
    
    /// <summary>
    /// Clears all move ordering tables.
    /// </summary>
    public void Clear()
    {
        Array.Clear(killerMoves);
        Array.Clear(historyTable);
        Array.Clear(counterMoves);
    }
}