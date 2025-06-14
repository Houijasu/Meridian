namespace Meridian.Core;

/// <summary>
/// Simplified move ordering that modifies move array in place
/// </summary>
public sealed class MoveOrderingSimple
{
    // Piece values for MVV-LVA
    private static ReadOnlySpan<int> PieceValues => [0, 100, 300, 300, 500, 900, 0];
    
    // Killer moves
    private const int MaxPly = 128;
    private readonly Move[,] _killerMoves = new Move[MaxPly, 2];
    
    // History heuristic
    private readonly int[,] _historyTable = new int[64, 64];
    
    // Move scores
    private const int HashMoveScore = 1_000_000;
    private const int GoodCaptureBaseScore = 100_000;
    private const int KillerMoveScore = 90_000;
    
    /// <summary>
    /// Score moves for ordering (returns scores array to use externally)
    /// </summary>
    public void ScoreMoves(ref BoardState board, Span<Move> moves, int moveCount, Span<int> scores, Move hashMove, int ply)
    {
        for (int i = 0; i < moveCount; i++)
        {
            scores[i] = ScoreMove(ref board, moves[i], hashMove, ply);
        }
    }
    
    /// <summary>
    /// Sort first N moves by score
    /// </summary>
    public static void PartialSort(Span<Move> moves, Span<int> scores, int count, int sortFirst = 10)
    {
        int limit = Math.Min(sortFirst, count - 1);
        
        for (int i = 0; i < limit; i++)
        {
            int bestIndex = i;
            int bestScore = scores[i];
            
            for (int j = i + 1; j < count; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestScore = scores[j];
                    bestIndex = j;
                }
            }
            
            if (bestIndex != i)
            {
                (moves[i], moves[bestIndex]) = (moves[bestIndex], moves[i]);
                (scores[i], scores[bestIndex]) = (scores[bestIndex], scores[i]);
            }
        }
    }
    
    private int ScoreMove(ref BoardState board, Move move, Move hashMove, int ply)
    {
        // Hash move
        if (move.Data == hashMove.Data)
            return HashMoveScore;
        
        // Captures - MVV-LVA
        if (move.IsCapture())
        {
            Piece victim = GetCapturedPiece(ref board, move);
            Piece attacker = GetPiece(ref board, move.From);
            
            int mvvLva = PieceValues[(int)victim] * 10 - PieceValues[(int)attacker];
            return GoodCaptureBaseScore + mvvLva;
        }
        
        // Killer moves
        if (ply < MaxPly && (move.Data == _killerMoves[ply, 0].Data || move.Data == _killerMoves[ply, 1].Data))
            return KillerMoveScore;
        
        // History heuristic
        return _historyTable[(int)move.From, (int)move.To];
    }
    
    private Piece GetCapturedPiece(ref BoardState board, Move move)
    {
        ulong toBitboard = 1UL << (int)move.To;
        ulong enemyPieces = board.SideToMove == Color.White ? board.BlackPieces : board.WhitePieces;
        
        if ((toBitboard & enemyPieces) == 0)
        {
            if (move.Type == MoveType.EnPassant)
                return Piece.Pawn;
            return Piece.None;
        }
        
        if (board.SideToMove == Color.White)
        {
            if ((toBitboard & board.BlackPawns) != 0) return Piece.Pawn;
            if ((toBitboard & board.BlackKnights) != 0) return Piece.Knight;
            if ((toBitboard & board.BlackBishops) != 0) return Piece.Bishop;
            if ((toBitboard & board.BlackRooks) != 0) return Piece.Rook;
            if ((toBitboard & board.BlackQueens) != 0) return Piece.Queen;
            if ((toBitboard & board.BlackKing) != 0) return Piece.King;
        }
        else
        {
            if ((toBitboard & board.WhitePawns) != 0) return Piece.Pawn;
            if ((toBitboard & board.WhiteKnights) != 0) return Piece.Knight;
            if ((toBitboard & board.WhiteBishops) != 0) return Piece.Bishop;
            if ((toBitboard & board.WhiteRooks) != 0) return Piece.Rook;
            if ((toBitboard & board.WhiteQueens) != 0) return Piece.Queen;
            if ((toBitboard & board.WhiteKing) != 0) return Piece.King;
        }
        
        return Piece.None;
    }
    
    private Piece GetPiece(ref BoardState board, Square square)
    {
        ulong bitboard = 1UL << (int)square;
        
        if ((bitboard & board.WhitePieces) != 0)
        {
            if ((bitboard & board.WhitePawns) != 0) return Piece.Pawn;
            if ((bitboard & board.WhiteKnights) != 0) return Piece.Knight;
            if ((bitboard & board.WhiteBishops) != 0) return Piece.Bishop;
            if ((bitboard & board.WhiteRooks) != 0) return Piece.Rook;
            if ((bitboard & board.WhiteQueens) != 0) return Piece.Queen;
            if ((bitboard & board.WhiteKing) != 0) return Piece.King;
        }
        else if ((bitboard & board.BlackPieces) != 0)
        {
            if ((bitboard & board.BlackPawns) != 0) return Piece.Pawn;
            if ((bitboard & board.BlackKnights) != 0) return Piece.Knight;
            if ((bitboard & board.BlackBishops) != 0) return Piece.Bishop;
            if ((bitboard & board.BlackRooks) != 0) return Piece.Rook;
            if ((bitboard & board.BlackQueens) != 0) return Piece.Queen;
            if ((bitboard & board.BlackKing) != 0) return Piece.King;
        }
        
        return Piece.None;
    }
    
    public void UpdateKillers(Move move, int ply)
    {
        if (ply >= MaxPly || move.IsCapture()) return;
        
        if (_killerMoves[ply, 0].Data == move.Data) return;
        
        _killerMoves[ply, 1] = _killerMoves[ply, 0];
        _killerMoves[ply, 0] = move;
    }
    
    public void UpdateHistory(Move move, int depth)
    {
        if (move.IsCapture()) return;
        
        _historyTable[(int)move.From, (int)move.To] += depth * depth;
        
        if (_historyTable[(int)move.From, (int)move.To] > 100_000)
            ScaleDownHistory();
    }
    
    private void ScaleDownHistory()
    {
        for (int i = 0; i < 64; i++)
            for (int j = 0; j < 64; j++)
                _historyTable[i, j] /= 2;
    }
    
    public void ClearKillers()
    {
        Array.Clear(_killerMoves);
    }
    
    public void AgeHistory()
    {
        for (int i = 0; i < 64; i++)
            for (int j = 0; j < 64; j++)
                _historyTable[i, j] = _historyTable[i, j] * 3 / 4;
    }
    
    public int GetHistoryScore(Move move)
    {
        return _historyTable[(int)move.From, (int)move.To];
    }
}