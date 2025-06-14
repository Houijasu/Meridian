namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Move ordering system for improving alpha-beta search efficiency
/// Implements MVV-LVA, killer moves, and history heuristic
/// </summary>
public sealed class MoveOrdering
{
    // Piece values for MVV-LVA (Most Valuable Victim - Least Valuable Attacker)
    private static ReadOnlySpan<int> PieceValues => [0, 100, 300, 300, 500, 900, 0]; // None, Pawn, Knight, Bishop, Rook, Queen, King
    
    // Killer moves - 2 per ply
    private const int MaxPly = 128;
    private const int KillersPerPly = 2;
    private readonly Move[,] _killerMoves = new Move[MaxPly, KillersPerPly];
    
    // History heuristic - indexed by [from][to]
    private readonly int[,] _historyTable = new int[64, 64];
    
    // Move scores for ordering
    private const int HashMoveScore = 1_000_000;
    private const int GoodCaptureBaseScore = 100_000;
    private const int KillerMoveScore = 90_000;
    private const int BadCaptureScore = -100_000;
    
    /// <summary>
    /// Create a new ordered move list for optimal alpha-beta cutoffs
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MoveList OrderMoves(ref BoardState board, ref MoveList moves, Move hashMove, int ply)
    {
        int moveCount = moves.Count;
        if (moveCount == 0)
            return new MoveList();
            
        // Limit move count to prevent overflow
        if (moveCount > 256)
        {
            Console.WriteLine($"Warning: Move count {moveCount} exceeds 256, clamping");
            moveCount = 256;
        }
            
        Span<int> scores = stackalloc int[moveCount];
        Span<Move> tempMoves = stackalloc Move[moveCount];
        
        // Copy moves and calculate scores
        for (int i = 0; i < moveCount; i++)
        {
            tempMoves[i] = moves[i];
            scores[i] = ScoreMove(ref board, moves[i], hashMove, ply);
        }
        
        // Selection sort for first few moves (good enough for small move lists)
        for (int i = 0; i < moveCount - 1; i++)
        {
            int bestIndex = i;
            int bestScore = scores[i];
            
            for (int j = i + 1; j < moveCount; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestScore = scores[j];
                    bestIndex = j;
                }
            }
            
            if (bestIndex != i)
            {
                // Swap moves and scores
                (tempMoves[i], tempMoves[bestIndex]) = (tempMoves[bestIndex], tempMoves[i]);
                (scores[i], scores[bestIndex]) = (scores[bestIndex], scores[i]);
            }
            
            // Only sort first 10 moves precisely, rest can be approximate
            if (i >= 10)
                break;
        }
        
        // Create new ordered move list
        MoveList orderedList = new();
        for (int i = 0; i < moveCount; i++)
        {
            orderedList.Add(tempMoves[i]);
        }
        
        return orderedList;
    }
    
    /// <summary>
    /// Score a single move for ordering
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ScoreMove(ref BoardState board, Move move, Move hashMove, int ply)
    {
        // Hash move gets highest priority
        if (move.Equals(hashMove))
            return HashMoveScore;
        
        // Score captures using MVV-LVA
        if (move.IsCapture())
        {
            return ScoreCapture(ref board, move);
        }
        
        // Check if it's a killer move
        if (IsKillerMove(move, ply))
            return KillerMoveScore;
        
        // Use history heuristic for quiet moves
        return _historyTable[(int)move.From, (int)move.To];
    }
    
    /// <summary>
    /// Score captures using MVV-LVA (Most Valuable Victim - Least Valuable Attacker)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ScoreCapture(ref BoardState board, Move move)
    {
        // Get victim piece type
        Piece victim = GetCapturedPiece(ref board, move);
        if (victim == Piece.None)
            return 0; // Not actually a capture
        
        // Get attacker piece type
        Piece attacker = GetPiece(ref board, move.From);
        
        // MVV-LVA: Prefer capturing valuable pieces with less valuable pieces
        int mvvLva = PieceValues[(int)victim] * 10 - PieceValues[(int)attacker];
        
        // Check if capture is safe using Static Exchange Evaluation (simplified)
        if (IsCaptureSafe(ref board, move))
        {
            return GoodCaptureBaseScore + mvvLva;
        }
        else
        {
            return BadCaptureScore + mvvLva;
        }
    }
    
    /// <summary>
    /// Simplified Static Exchange Evaluation to determine if a capture is safe
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCaptureSafe(ref BoardState board, Move move)
    {
        // For now, use a simplified version:
        // - Pawn captures are always considered safe
        // - Equal or winning exchanges are safe
        // - Under-promotions are not safe
        
        Piece attacker = GetPiece(ref board, move.From);
        Piece victim = GetCapturedPiece(ref board, move);
        
        // Pawn captures are usually safe
        if (attacker == Piece.Pawn)
            return true;
        
        // Equal or winning exchanges
        if (PieceValues[(int)victim] >= PieceValues[(int)attacker])
            return true;
        
        // TODO: Implement full SEE for accurate exchange evaluation
        // For now, assume defended pieces make captures unsafe
        return !IsSquareDefended(ref board, move.To, board.SideToMove.Opposite());
    }
    
    /// <summary>
    /// Check if a square is defended by the given side
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsSquareDefended(ref BoardState board, Square square, Color bySide)
    {
        return Attacks.IsSquareAttacked(ref board, square, bySide);
    }
    
    /// <summary>
    /// Get the type of piece being captured
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Piece GetCapturedPiece(ref BoardState board, Move move)
    {
        ulong toBitboard = 1UL << (int)move.To;
        ulong enemyPieces = board.SideToMove == Color.White ? board.BlackPieces : board.WhitePieces;
        
        if ((toBitboard & enemyPieces) == 0)
        {
            // Check for en passant capture
            if (move.Type == MoveType.EnPassant)
                return Piece.Pawn;
            return Piece.None;
        }
        
        // Determine piece type on target square
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
    
    /// <summary>
    /// Get the type of piece on a square
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Piece GetPiece(ref BoardState board, Square square)
    {
        ulong bitboard = 1UL << (int)square;
        
        // Check white pieces
        if ((bitboard & board.WhitePieces) != 0)
        {
            if ((bitboard & board.WhitePawns) != 0) return Piece.Pawn;
            if ((bitboard & board.WhiteKnights) != 0) return Piece.Knight;
            if ((bitboard & board.WhiteBishops) != 0) return Piece.Bishop;
            if ((bitboard & board.WhiteRooks) != 0) return Piece.Rook;
            if ((bitboard & board.WhiteQueens) != 0) return Piece.Queen;
            if ((bitboard & board.WhiteKing) != 0) return Piece.King;
        }
        // Check black pieces
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
    
    /// <summary>
    /// Check if a move is a killer move for the given ply
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsKillerMove(Move move, int ply)
    {
        if (ply >= MaxPly) return false;
        
        for (int i = 0; i < KillersPerPly; i++)
        {
            if (_killerMoves[ply, i].Equals(move))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Update killer moves when a quiet move causes a beta cutoff
    /// </summary>
    public void UpdateKillers(Move move, int ply)
    {
        if (ply >= MaxPly || move.IsCapture()) return;
        
        // Don't store the same move twice
        if (_killerMoves[ply, 0].Equals(move)) return;
        
        // Shift killer moves and add new one
        _killerMoves[ply, 1] = _killerMoves[ply, 0];
        _killerMoves[ply, 0] = move;
    }
    
    /// <summary>
    /// Update history heuristic for a move that caused a cutoff
    /// </summary>
    public void UpdateHistory(Move move, int depth)
    {
        if (move.IsCapture()) return;
        
        // Increase history score based on depth
        _historyTable[(int)move.From, (int)move.To] += depth * depth;
        
        // Prevent overflow by scaling down all values if needed
        if (_historyTable[(int)move.From, (int)move.To] > 100_000)
        {
            ScaleDownHistory();
        }
    }
    
    /// <summary>
    /// Scale down all history values to prevent overflow
    /// </summary>
    private void ScaleDownHistory()
    {
        for (int from = 0; from < 64; from++)
        {
            for (int to = 0; to < 64; to++)
            {
                _historyTable[from, to] /= 2;
            }
        }
    }
    
    /// <summary>
    /// Clear killer moves for a new search
    /// </summary>
    public void ClearKillers()
    {
        Array.Clear(_killerMoves);
    }
    
    /// <summary>
    /// Age history table between searches
    /// </summary>
    public void AgeHistory()
    {
        for (int from = 0; from < 64; from++)
        {
            for (int to = 0; to < 64; to++)
            {
                _historyTable[from, to] = _historyTable[from, to] * 3 / 4;
            }
        }
    }
}