namespace Meridian.Core;

using System.Runtime.CompilerServices;

public sealed class Search
{
    // Search constants
    private const int MaxDepth = 64;
    private const int Infinity = 1000000;
    private const int MateScore = 100000;
    private const int DrawScore = 0;
    
    // Search statistics
    public ulong NodesSearched { get; private set; }
    public int SelectiveDepth { get; private set; }
    
    // Best move from the search
    public Move BestMove { get; private set; }
    public int BestScore { get; private set; }
    
    // Time management
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private long _timeLimit;
    private bool _shouldStop;
    
    // Transposition table
    private readonly TranspositionTable _tt;
    
    // Move ordering
    private readonly MoveOrderingSimple _moveOrdering;
    
    public Search()
    {
        _stopwatch = new System.Diagnostics.Stopwatch();
        _tt = new TranspositionTable(128); // 128 MB default
        _moveOrdering = new MoveOrderingSimple();
    }
    
    public Move FindBestMove(ref BoardState board, int depthLimit, long timeLimitMs = long.MaxValue)
    {
        NodesSearched = 0;
        SelectiveDepth = 0;
        BestMove = default;
        BestScore = -Infinity;
        _shouldStop = false;
        _timeLimit = timeLimitMs;
        
        // Clear killer moves for new search
        _moveOrdering.ClearKillers();
        // Age history table
        _moveOrdering.AgeHistory();
        
        _stopwatch.Restart();
        
        // Iterative deepening
        for (int depth = 1; depth <= depthLimit && !_shouldStop; depth++)
        {
            int score = AlphaBeta(ref board, depth, -Infinity, Infinity, true, 0);
            
            if (!_shouldStop)
            {
                BestScore = score;
                
                // Print search info
                long timeMs = _stopwatch.ElapsedMilliseconds;
                if (timeMs > 0)
                {
                    ulong nps = (NodesSearched * 1000) / (ulong)timeMs;
                    Console.WriteLine($"info depth {depth} score cp {score} nodes {NodesSearched} nps {nps} time {timeMs} " +
                                    $"hashfull {(int)(_tt.FillRate() * 10)} hits {_tt.Hits} pv {BestMove}");
                }
            }
            
            // Time management - stop if we've used more than 40% of our time
            if (_stopwatch.ElapsedMilliseconds > _timeLimit * 0.4)
                break;
        }
        
        return BestMove;
    }
    
    private int AlphaBeta(ref BoardState board, int depth, int alpha, int beta, bool isRoot, int ply = 0)
    {
        // Prevent stack overflow
        if (ply >= MaxDepth - 1)
            return Evaluation.Evaluate(ref board);
            
        if (_shouldStop || _stopwatch.ElapsedMilliseconds > _timeLimit)
        {
            _shouldStop = true;
            return 0;
        }
        
        NodesSearched++;
        
        // Check for draw by repetition or 50-move rule
        if (!isRoot && (board.HalfMoveClock >= 100 || IsRepetition(ref board)))
            return DrawScore;
        
        // Compute hash for this position
        ulong hash = Zobrist.ComputeHash(ref board);
        int originalAlpha = alpha;
        
        // Probe transposition table
        Move ttMove = default;
        if (!isRoot && _tt.Probe(hash, depth, alpha, beta, out TTEntry ttEntry))
        {
            ttMove = ttEntry.Move;
            
            // Use TT score if applicable
            if (ttEntry.Flags == (byte)TTFlags.Exact)
            {
                return ttEntry.Score;
            }
            else if (ttEntry.Flags == (byte)TTFlags.LowerBound)
            {
                alpha = Math.Max(alpha, ttEntry.Score);
            }
            else if (ttEntry.Flags == (byte)TTFlags.UpperBound)
            {
                beta = Math.Min(beta, ttEntry.Score);
            }
            
            if (alpha >= beta)
                return ttEntry.Score;
        }
        
        // Leaf node - return evaluation
        if (depth == 0)
            return Quiescence(ref board, alpha, beta);
            
        // Null move pruning
        if (!isRoot && depth >= 3 && !IsKingInCheck(ref board, board.SideToMove))
        {
            // Make null move (just switch sides)
            BoardState nullBoard = board;
            nullBoard.SideToMove = nullBoard.SideToMove.Opposite();
            nullBoard.EnPassantSquare = Square.None;
            nullBoard.Hash = Zobrist.ToggleSideToMove(nullBoard.Hash);
            if (board.EnPassantSquare != Square.None)
                nullBoard.Hash = Zobrist.ToggleEnPassant(nullBoard.Hash, board.EnPassantSquare, Square.None);
            
            // Search with reduced depth (R=2 or R=3)
            int R = depth >= 6 ? 3 : 2;
            int nullScore = -AlphaBeta(ref nullBoard, depth - R - 1, -beta, -beta + 1, false, ply + 1);
            
            // If null move fails high, we can prune
            if (nullScore >= beta)
            {
                // Avoid zugzwang in endgames with few pieces
                int pieceCount = Bitboard.PopCount(board.AllPieces);
                if (pieceCount > 7) // Not an endgame
                    return beta;
            }
        }
        
        // Generate all legal moves
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        // For now, skip move ordering to avoid ref struct issues
        // TODO: Implement move ordering without ref struct complications
        
        // Filter out illegal moves and count legal ones
        int legalMoves = 0;
        Move bestMoveInPosition = default;
        
        for (int i = 0; i < moves.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            // Skip if move leaves king in check
            if (IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                board = copy;
                continue;
            }
            
            legalMoves++;
            
            // Late Move Reductions (LMR)
            int newDepth = depth - 1;
            bool doFullSearch = true;
            
            // Apply LMR for late quiet moves
            if (depth >= 3 && legalMoves > 3 && !moves[i].IsCapture() && !IsKingInCheck(ref board, board.SideToMove))
            {
                // Reduce depth for late quiet moves
                int reduction = 1;
                if (legalMoves > 6) reduction = 2;
                if (depth >= 6 && legalMoves > 12) reduction = 3;
                
                newDepth = Math.Max(1, depth - 1 - reduction);
                
                // Search with reduced depth
                int score = -AlphaBeta(ref board, newDepth, -alpha - 1, -alpha, false, ply + 1);
                
                // If the move fails high, research at full depth
                if (score > alpha)
                {
                    newDepth = depth - 1;
                }
                else
                {
                    doFullSearch = false;
                }
            }
            
            // Full depth search
            int finalScore = 0;
            if (doFullSearch)
            {
                finalScore = -AlphaBeta(ref board, newDepth, -beta, -alpha, false, ply + 1);
            }
            else
            {
                // Use the LMR score
                finalScore = -AlphaBeta(ref board, newDepth, -alpha - 1, -alpha, false, ply + 1);
            }
            
            board = copy;
            
            if (_shouldStop)
                return 0;
            
            // Update best move
            if (finalScore > alpha)
            {
                alpha = finalScore;
                bestMoveInPosition = moves[i];
                
                if (isRoot)
                {
                    BestMove = moves[i];
                }
                
                // Beta cutoff
                if (alpha >= beta)
                {
                    // Update move ordering heuristics
                    if (!moves[i].IsCapture())
                    {
                        _moveOrdering.UpdateKillers(moves[i], ply);
                        _moveOrdering.UpdateHistory(moves[i], depth);
                    }
                    break;
                }
            }
        }
        
        // No legal moves - checkmate or stalemate
        if (legalMoves == 0)
        {
            if (IsKingInCheck(ref board, board.SideToMove))
                return -MateScore + (MaxDepth - depth); // Checkmate
            else
                return DrawScore; // Stalemate
        }
        
        // Store in transposition table
        TTFlags flags = TTFlags.UpperBound;
        if (alpha >= beta)
        {
            flags = TTFlags.LowerBound;
        }
        else if (alpha > originalAlpha)
        {
            flags = TTFlags.Exact;
        }
        
        _tt.Store(hash, bestMoveInPosition, alpha, depth, flags);
        
        return alpha;
    }
    
    private int Quiescence(ref BoardState board, int alpha, int beta)
    {
        NodesSearched++;
        
        // Stand pat score
        int standPat = Evaluation.Evaluate(ref board);
        
        if (standPat >= beta)
            return beta;
        
        if (standPat > alpha)
            alpha = standPat;
        
        // Generate only captures
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        // For now, search captures without ordering
        // TODO: Implement capture ordering without ref struct issues
        
        for (int i = 0; i < moves.Count; i++)
        {
            // Only search captures
            if (!moves[i].IsCapture())
                continue;
                
            BoardState copy = board;
            board.MakeMove(moves[i]);
            
            // Skip if move leaves king in check
            if (IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                board = copy;
                continue;
            }
            
            int score = -Quiescence(ref board, -beta, -alpha);
            
            board = copy;
            
            if (score >= beta)
                return beta;
            
            if (score > alpha)
                alpha = score;
        }
        
        return alpha;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
    
    private static bool IsRepetition(ref BoardState board)
    {
        // Simple repetition detection - would need position history in real implementation
        // For now, return false
        return false;
    }
    
    
    public void Stop()
    {
        _shouldStop = true;
    }
    
    public void ClearTT()
    {
        _tt.Clear();
    }
}