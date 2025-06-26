#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;
using Meridian.Core.Evaluation;
using Meridian.Core.MoveGeneration;

namespace Meridian.Core.Search;

public sealed class SearchThread : IDisposable
{
    private readonly int _threadId;
    private readonly TranspositionTable _transpositionTable;
    private readonly ThreadPool _threadPool;
    private readonly ThreadData _threadData;
    private readonly MoveGenerator _moveGenerator;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _searchSignal;
    private readonly ManualResetEventSlim _completeSignal;
    
    private Position? _rootPosition;
    private SearchLimits? _limits;
    private int _depthOffset;
    private DateTime _startTime;
    private int _allocatedTime;
    private volatile bool _shouldStop;
    private volatile bool _shouldExit;
    
    public SearchThread(int threadId, TranspositionTable transpositionTable, ThreadPool threadPool)
    {
        _threadId = threadId;
        _transpositionTable = transpositionTable;
        _threadPool = threadPool;
        _threadData = new ThreadData(threadId);
        _moveGenerator = new MoveGenerator();
        _searchSignal = new ManualResetEventSlim(false);
        _completeSignal = new ManualResetEventSlim(true);
        
        _thread = new Thread(WorkerLoop)
        {
            Name = $"SearchThread_{threadId}",
            IsBackground = true
        };
        _thread.Start();
    }
    
    public void StartSearch(Position position, SearchLimits limits, int depthOffset = 0)
    {
        if (position == null || limits == null) return;
        _rootPosition = new Position(position);
        _limits = limits;
        _depthOffset = depthOffset;
        _shouldStop = false;
        _startTime = DateTime.UtcNow;
        _allocatedTime = CalculateSearchTime(limits, position);
        
        _threadData.Clear();
        
        _completeSignal.Reset();
        _searchSignal.Set();
    }
    
    public void Stop()
    {
        _shouldStop = true;
        _threadData.ShouldStop = true;
    }
    
    public void WaitForSearchComplete()
    {
        _completeSignal.Wait();
    }
    
    public void Dispose()
    {
        _shouldExit = true;
        _shouldStop = true;
        _searchSignal.Set();
        _thread.Join(5000);
        _searchSignal.Dispose();
        _completeSignal.Dispose();
    }
    
    private void WorkerLoop()
    {
        while (!_shouldExit)
        {
            _searchSignal.Wait();
            _searchSignal.Reset();
            
            if (_shouldExit)
                break;
                
            if (_rootPosition != null && _limits != null)
            {
                RunSearch();
            }
            
            _completeSignal.Set();
        }
    }
    
    private void RunSearch()
    {
        var position = _rootPosition!;
        var limits = _limits!;
        Move bestMove = Move.None;
        var maxDepth = limits.Depth > 0 ? Math.Min(limits.Depth, SearchConstants.MaxDepth) : SearchConstants.MaxDepth;
        
        // Apply depth offset for helper threads
        var startDepth = 1 + _depthOffset;
        
        for (var depth = startDepth; depth <= maxDepth && !_shouldStop; depth++)
        {
            var score = 0;
            
            // Use aspiration windows for main thread
            if (_threadId == 0 && depth >= 5 && _threadData.Info.Score != 0)
            {
                var aspirationDelta = 25;
                var alpha = _threadData.Info.Score - aspirationDelta;
                var beta = _threadData.Info.Score + aspirationDelta;
                
                score = Search(position, depth, alpha, beta, 0);
                
                // Re-search if we fall outside aspiration window
                if (score <= alpha || score >= beta)
                {
                    aspirationDelta *= 2;
                    alpha = score <= alpha ? -SearchConstants.Infinity : _threadData.Info.Score - aspirationDelta;
                    beta = score >= beta ? SearchConstants.Infinity : _threadData.Info.Score + aspirationDelta;
                    score = Search(position, depth, alpha, beta, 0);
                }
            }
            else
            {
                score = Search(position, depth, -SearchConstants.Infinity, SearchConstants.Infinity, 0);
            }
            
            if (_shouldStop)
                break;
                
            bestMove = _threadData.Info.PrincipalVariation.Count > 0 ? _threadData.Info.PrincipalVariation[0] : Move.None;
            
            _threadData.Info.Depth = depth;
            _threadData.Info.Score = score;
            _threadData.Info.Time = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
            
            // Update thread pool with best move
            if (bestMove != Move.None)
            {
                _threadPool.UpdateBestMove(bestMove, score, _threadData);
            }
            
            if (Math.Abs(score) >= SearchConstants.MateScore - SearchConstants.MaxDepth)
            {
                break;
            }
            
            // Only main thread checks time
            if (_threadId == 0 && ShouldStopOnTime())
            {
                _threadPool.StopSearch();
                break;
            }
        }
    }
    
    private int Search(Position position, int depth, int alpha, int beta, int ply, bool allowNull = true)
    {
        if (_shouldStop || _threadPool.IsSearchStopped())
            return 0;
            
        _threadData.Info.Nodes++;
        
        var isPvNode = beta - alpha > 1;
        _threadData.PvLength[ply] = 0;
        
        if (ply > 0 && position.IsDraw())
            return 0;
            
        // Transposition table probe
        var alphaOrig = alpha;
        Move ttMove = Move.None;
        
        if (_transpositionTable.Probe(position.ZobristKey, depth, alpha, beta, ply, out var ttScore, out ttMove))
        {
            return ttScore;
        }
        
        if (depth <= 0)
            return Quiescence(position, alpha, beta, ply);
            
        if (ply >= SearchConstants.MaxDepth)
            return Evaluator.Evaluate(position);
            
        CheckTimeLimit();
        
        var ourKing = GetKingSquare(position, position.SideToMove);
        var inCheck = ourKing != Square.None && MoveGenerator.IsSquareAttacked(position, 
            ourKing, 
            position.SideToMove == Color.White ? Color.Black : Color.White);
        
        // Check extension
        if (inCheck)
            depth++;
        
        var staticEval = Evaluator.Evaluate(position);
        
        // Null move pruning
        if (allowNull && !inCheck && ply > 0 && depth >= 3 && !isPvNode &&
            staticEval >= beta && HasNonPawnMaterial(position, position.SideToMove))
        {
            var reduction = 3 + depth / 4 + Math.Min((staticEval - beta) / 200, 3);
            var nullPosition = new Position(position);
            nullPosition.MakeNullMove();
            
            var nullScore = -Search(nullPosition, depth - reduction - 1, -beta, -beta + 1, ply + 1, false);
            
            if (nullScore >= beta)
            {
                if (Math.Abs(nullScore) >= SearchConstants.MateInMaxPly)
                    return beta;
                    
                if (depth >= 12)
                {
                    var verifyScore = Search(position, depth - reduction - 1, beta - 1, beta, ply + 1, false);
                    if (verifyScore >= beta)
                        return verifyScore;
                }
                else
                {
                    return nullScore;
                }
            }
        }
        
        // Futility pruning
        var futilityMargin = 0;
        if (!isPvNode && !inCheck && depth <= 3)
        {
            futilityMargin = FutilityMargin(depth);
            if (staticEval + futilityMargin <= alpha)
            {
                var score = Quiescence(position, alpha, beta, ply);
                if (score <= alpha)
                    return score;
            }
        }
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        if (moves.Count == 0)
        {
            return inCheck ? -SearchConstants.MateScore + ply : 0;
        }
        
        OrderMoves(ref moves, position, ttMove, ply);
        
        Move bestMove = Move.None;
        var bestScore = -SearchConstants.Infinity;
        
        var movesSearched = 0;
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var newPosition = new Position(position);
            newPosition.MakeMove(move);
            
            var score = 0;
            var newDepth = depth - 1;
            
            var opponentKing = GetKingSquare(newPosition, newPosition.SideToMove);
            var givesCheck = opponentKing != Square.None && MoveGenerator.IsSquareAttacked(newPosition, 
                opponentKing, 
                newPosition.SideToMove == Color.White ? Color.Black : Color.White);
            
            // Late move reductions (LMR)
            if (movesSearched >= 4 && depth >= 3 && !inCheck && !givesCheck &&
                !move.IsCapture && !move.IsPromotion)
            {
                var reduction = GetLMRReduction(depth, movesSearched, isPvNode);
                
                if (_threadData.GetHistoryScore(move, position.SideToMove) > 0)
                    reduction = Math.Max(1, reduction - 1);
                    
                newDepth = Math.Max(1, newDepth - reduction);
            }
            
            // Principal Variation Search (PVS)
            if (movesSearched == 0)
            {
                // First move is always searched with a full window
                score = -Search(newPosition, newDepth, -beta, -alpha, ply + 1);
            }
            else
            {
                // All subsequent moves are searched with a zero-window to test if they are better than the current best
                score = -Search(newPosition, newDepth, -alpha - 1, -alpha, ply + 1);
                
                // If the zero-window search returned a score better than alpha, it means this move
                // might be the new best move. We must re-search it with a full window to get an accurate score.
                // This re-search is only necessary at PV nodes.
                if (score > alpha && score < beta && isPvNode)
                {
                    _threadData.Info.PvsReSearches++;
                    score = -Search(newPosition, newDepth, -beta, -alpha, ply + 1);
                }
                else if (score <= alpha)
                {
                    _threadData.Info.PvsHits++;
                }
            }
            
            movesSearched++;
            
            if (_shouldStop || _threadPool.IsSearchStopped())
                return 0;
                
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
                
                if (score > alpha)
                {
                    alpha = score;
                    
                    _threadData.UpdatePrincipalVariation(move, ply);
                        
                    if (score >= beta)
                    {
                        if (!move.IsCapture)
                            _threadData.UpdateKillerMoves(move, ply);
                        break;
                    }
                }
            }
        }
        
        // Update history for all quiet moves that were searched
        if (bestScore >= beta && !bestMove.IsCapture)
        {
            _threadData.UpdateHistoryScore(bestMove, depth * depth, position.SideToMove);
            
            for (var i = 0; i < movesSearched - 1; i++)
            {
                var move = moves[i];
                if (!move.IsCapture && move != bestMove)
                    _threadData.UpdateHistoryScore(move, -depth * depth, position.SideToMove);
            }
        }
        
        // Store in transposition table
        var nodeType = bestScore <= alphaOrig ? NodeType.UpperBound :
                      bestScore >= beta ? NodeType.LowerBound :
                      NodeType.Exact;
        
        _transpositionTable.Store(position.ZobristKey, bestScore, bestMove, depth, nodeType, ply);
        
        return bestScore;
    }
    
    private int Quiescence(Position position, int alpha, int beta, int ply)
    {
        if (_shouldStop || _threadPool.IsSearchStopped())
            return 0;
            
        _threadData.Info.Nodes++;
        
        if (ply >= SearchConstants.MaxDepth)
            return Evaluator.Evaluate(position);
        
        var standPat = Evaluator.Evaluate(position);
        
        if (standPat >= beta)
            return beta;
            
        if (standPat > alpha)
            alpha = standPat;
            
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Span<Move> captureBuffer = stackalloc Move[64];
        var captures = new MoveList(captureBuffer);
        ExtractCaptures(ref moves, ref captures);
        OrderCaptures(ref captures, position);
        
        for (var i = 0; i < captures.Count; i++)
        {
            var move = captures[i];
            
            // Delta pruning
            var captureValue = GetPieceValue(move.CapturedPiece.Type());
            if (standPat + captureValue + 200 < alpha && move.PromotionType == PieceType.None)
                continue;
            
            var newPosition = new Position(position);
            newPosition.MakeMove(move);
            
            var score = -Quiescence(newPosition, -beta, -alpha, ply + 1);
            
            if (_shouldStop || _threadPool.IsSearchStopped())
                return 0;
                
            if (score >= beta)
                return beta;
                
            if (score > alpha)
                alpha = score;
        }
        
        return alpha;
    }
    
    private void OrderMoves(ref MoveList moves, Position position, Move ttMove, int ply)
    {
        Span<int> scores = stackalloc int[moves.Count];
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            
            if (move == ttMove)
                scores[i] = 1_000_000;  // TT move has highest priority
            else if (move.IsCapture)
                scores[i] = ScoreCapture(move, position) + 100_000;
            else if (_threadData.IsKillerMove(move, ply))
                scores[i] = 90_000;
            else
                scores[i] = _threadData.GetHistoryScore(move, position.SideToMove);
        }
        
        for (var i = 0; i < moves.Count - 1; i++)
        {
            var bestIndex = i;
            var bestScore = scores[i];
            
            for (var j = i + 1; j < moves.Count; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestScore = scores[j];
                    bestIndex = j;
                }
            }
            
            if (bestIndex != i)
            {
                SwapMoves(ref moves, i, bestIndex);
                
                var tempScore = scores[i];
                scores[i] = scores[bestIndex];
                scores[bestIndex] = tempScore;
            }
        }
    }
    
    private void OrderCaptures(ref MoveList captures, Position position)
    {
        Span<int> scores = stackalloc int[captures.Count];
        
        for (var i = 0; i < captures.Count; i++)
        {
            scores[i] = ScoreCapture(captures[i], position);
        }
        
        for (var i = 0; i < captures.Count - 1; i++)
        {
            var bestIndex = i;
            var bestScore = scores[i];
            
            for (var j = i + 1; j < captures.Count; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestScore = scores[j];
                    bestIndex = j;
                }
            }
            
            if (bestIndex != i)
            {
                var tempMove = captures[i];
                captures.Set(i, captures[bestIndex]);
                captures.Set(bestIndex, tempMove);
                
                var tempScore = scores[i];
                scores[i] = scores[bestIndex];
                scores[bestIndex] = tempScore;
            }
        }
    }
    
    private static int ScoreCapture(Move move, Position position)
    {
        var victim = GetPieceValue(move.CapturedPiece.Type());
        var attackerPiece = position.GetPiece(move.From);
        var attacker = GetPieceValue(attackerPiece.Type());
        return victim * 10 - attacker;
    }
    
    private static int GetPieceValue(PieceType type) => type switch
    {
        PieceType.Pawn => PieceValues.Pawn,
        PieceType.Knight => PieceValues.Knight,
        PieceType.Bishop => PieceValues.Bishop,
        PieceType.Rook => PieceValues.Rook,
        PieceType.Queen => PieceValues.Queen,
        PieceType.King => PieceValues.King,
        _ => 0
    };
    
    private static void ExtractCaptures(ref MoveList allMoves, ref MoveList captures)
    {
        for (var i = 0; i < allMoves.Count; i++)
        {
            var move = allMoves[i];
            if (move.IsCapture || move.IsPromotion)
                captures.Add(move);
        }
    }
    
    private static Square GetKingSquare(Position position, Color color)
    {
        var king = position.GetBitboard(color, PieceType.King);
        return king.IsEmpty() ? Square.None : (Square)king.GetLsbIndex();
    }
    
    private static int CalculateSearchTime(SearchLimits limits, Position position)
    {
        if (limits.MoveTime > 0)
            return limits.MoveTime;
            
        if (limits.Infinite || limits.Depth > 0)
            return int.MaxValue;
            
        var timeLeft = position.SideToMove == Color.White ? limits.WhiteTime : limits.BlackTime;
        var increment = position.SideToMove == Color.White ? limits.WhiteIncrement : limits.BlackIncrement;
        
        if (timeLeft <= 0)
            return 100;
            
        var movesToGo = limits.MovesToGo > 0 ? limits.MovesToGo : 40;
        var baseTime = timeLeft / movesToGo;
        var totalTime = baseTime + increment * 3 / 4;
        
        return Math.Min(totalTime, timeLeft - 50);
    }
    
    private void CheckTimeLimit()
    {
        if ((_threadData.Info.Nodes & 1023) == 0)
        {
            if (ShouldStopOnTime())
                _shouldStop = true;
        }
    }
    
    private bool ShouldStopOnTime()
    {
        var elapsed = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
        return elapsed >= _allocatedTime;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapMoves(ref MoveList moves, int i, int j)
    {
        var temp = moves[i];
        moves.Set(i, moves[j]);
        moves.Set(j, temp);
    }
    
    private static bool HasNonPawnMaterial(Position position, Color color)
    {
        return position.GetBitboard(color, PieceType.Knight).IsNotEmpty() ||
               position.GetBitboard(color, PieceType.Bishop).IsNotEmpty() ||
               position.GetBitboard(color, PieceType.Rook).IsNotEmpty() ||
               position.GetBitboard(color, PieceType.Queen).IsNotEmpty();
    }
    
    private static int FutilityMargin(int depth) => depth * 100;
    
    private static int GetLMRReduction(int depth, int moveNumber, bool isPvNode)
    {
        if (depth < 3 || moveNumber < 4)
            return 0;
            
        var reduction = (int)(0.75 + Math.Log(depth) * Math.Log(moveNumber) / 2.25);
        
        if (!isPvNode)
            reduction++;
            
        return Math.Min(reduction, depth - 2);
    }
}