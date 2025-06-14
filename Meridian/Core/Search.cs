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
    
    public Search()
    {
        _stopwatch = new System.Diagnostics.Stopwatch();
    }
    
    public Move FindBestMove(ref BoardState board, int depthLimit, long timeLimitMs = long.MaxValue)
    {
        NodesSearched = 0;
        SelectiveDepth = 0;
        BestMove = default;
        BestScore = -Infinity;
        _shouldStop = false;
        _timeLimit = timeLimitMs;
        
        _stopwatch.Restart();
        
        // Iterative deepening
        for (int depth = 1; depth <= depthLimit && !_shouldStop; depth++)
        {
            int score = AlphaBeta(ref board, depth, -Infinity, Infinity, true);
            
            if (!_shouldStop)
            {
                BestScore = score;
                
                // Print search info
                long timeMs = _stopwatch.ElapsedMilliseconds;
                if (timeMs > 0)
                {
                    ulong nps = (NodesSearched * 1000) / (ulong)timeMs;
                    Console.WriteLine($"info depth {depth} score cp {score} nodes {NodesSearched} nps {nps} time {timeMs} pv {BestMove}");
                }
            }
            
            // Time management - stop if we've used more than 40% of our time
            if (_stopwatch.ElapsedMilliseconds > _timeLimit * 0.4)
                break;
        }
        
        return BestMove;
    }
    
    private int AlphaBeta(ref BoardState board, int depth, int alpha, int beta, bool isRoot)
    {
        if (_shouldStop || _stopwatch.ElapsedMilliseconds > _timeLimit)
        {
            _shouldStop = true;
            return 0;
        }
        
        NodesSearched++;
        
        // Check for draw by repetition or 50-move rule
        if (!isRoot && (board.HalfMoveClock >= 100 || IsRepetition(ref board)))
            return DrawScore;
        
        // Leaf node - return evaluation
        if (depth == 0)
            return Quiescence(ref board, alpha, beta);
        
        // Generate all legal moves
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
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
            
            // Recursive search
            int score = -AlphaBeta(ref board, depth - 1, -beta, -alpha, false);
            
            board = copy;
            
            if (_shouldStop)
                return 0;
            
            // Update best move
            if (score > alpha)
            {
                alpha = score;
                bestMoveInPosition = moves[i];
                
                if (isRoot)
                {
                    BestMove = moves[i];
                }
                
                // Beta cutoff
                if (alpha >= beta)
                    break;
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
}