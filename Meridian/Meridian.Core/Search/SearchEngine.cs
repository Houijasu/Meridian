#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;
using Meridian.Core.Evaluation;
using Meridian.Core.MoveGeneration;

namespace Meridian.Core.Search;

public sealed class SearchEngine
{
    private readonly MoveGenerator _moveGenerator = new();
    private readonly SearchInfo _info = new();
    private TranspositionTable _transpositionTable;
    private Position _rootPosition = null!;
    private volatile bool _shouldStop;
    private DateTime _startTime;
    private int _allocatedTime;
    private SearchLimits _currentLimits = null!;
    private int _maxPly;  // Track maximum ply reached (selective depth)
    private long _lastProgressNodes;  // Track last node count for progress reporting
    
    // Thread-specific parameters for Lazy SMP
    private int _threadId;
    private int _aspirationWindowAdjustment;
    
    public SearchInfo SearchInfo => _info;
    public Action<SearchInfo>? OnSearchProgress { get; set; }
    
    public SearchEngine(int ttSizeMb = 128)
    {
        _transpositionTable = new TranspositionTable(ttSizeMb);
    }
    
    // Constructor for shared transposition table (Lazy SMP)
    public SearchEngine(TranspositionTable sharedTT)
    {
        _transpositionTable = sharedTT ?? throw new ArgumentNullException(nameof(sharedTT));
    }
    
    public Move StartSearch(Position position, SearchLimits limits)
    {
        if (position == null || limits == null) return Move.None;
        
        _rootPosition = new Position(position);
        _shouldStop = false;
        _startTime = DateTime.UtcNow;
        _currentLimits = limits;
        _allocatedTime = CalculateSearchTime(limits, position);
        
        _info.Clear();
        _transpositionTable.NewSearch();
        _maxPly = 0;
        _lastProgressNodes = 0;
        
        // Clear PV arrays - properly initialize the 2D array
        for (int i = 0; i < SearchConstants.MaxPly; i++)
        {
            for (int j = 0; j < SearchConstants.MaxPly; j++)
            {
                _pvTable[i, j] = Move.None;
            }
            _pvLength[i] = 0;
        }
        
        Move bestMove = Move.None;
        var maxDepth = limits.Depth > 0 ? Math.Min(limits.Depth, SearchConstants.MaxDepth) : SearchConstants.MaxDepth;
        
        
        var aspirationDelta = 25 + _aspirationWindowAdjustment;
        var alpha = -SearchConstants.Infinity;
        var beta = SearchConstants.Infinity;
        
        for (var depth = 1; depth <= maxDepth && !_shouldStop; depth++)
        {
            // Report depth start
            _info.Depth = depth;
            _info.SelectiveDepth = Math.Max(_maxPly, 1);
            _info.Time = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
            OnSearchProgress?.Invoke(_info);
            
            // Aspiration windows
            if (depth >= 5 && _info.Score != 0)
            {
                alpha = _info.Score - aspirationDelta;
                beta = _info.Score + aspirationDelta;
            }
            
            var score = Search(position, depth, alpha, beta, 0);
            
            
            
            // Re-search if we fall outside aspiration window
            if (score <= alpha || score >= beta)
            {
                aspirationDelta *= 2;
                alpha = score <= alpha ? -SearchConstants.Infinity : _info.Score - aspirationDelta;
                beta = score >= beta ? SearchConstants.Infinity : _info.Score + aspirationDelta;
                score = Search(position, depth, alpha, beta, 0);
            }
            
            if (_shouldStop)
                break;
                
            // Get the best move from the PV table
            if (_pvLength[0] > 0 && _pvTable[0, 0] != Move.None)
            {
                bestMove = _pvTable[0, 0];
            }
            else
            {
                bestMove = _info.PrincipalVariation.TryPeek(out var pvMove) ? pvMove : Move.None;
            }
            
            _info.Depth = depth;
            _info.Score = score;
            _info.SelectiveDepth = Math.Max(_maxPly, 1);
            _info.Time = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
            
            // Report progress at depth completion
            OnSearchProgress?.Invoke(_info);
            
            if (Math.Abs(score) >= SearchConstants.MateScore - SearchConstants.MaxPly)
            {
                break;
            }
            
            // Only check time if we're not searching to a specific depth
            if (_currentLimits.Depth == 0 && ShouldStopOnTime())
            {
                break;
            }
            
        }
        
        return bestMove;
    }
    
    public void Stop()
    {
        _shouldStop = true;
    }
    
    private int Search(Position position, int depth, int alpha, int beta, int ply, bool allowNull = true)
    {
        // CRITICAL: Check ply bounds FIRST - before ANY other operation
        if (ply >= SearchConstants.MaxPly - 1)
            return Evaluator.Evaluate(position);
            
        if (_shouldStop)
            return 0;
        
        // Already checked ply bounds at function entry
            
        _info.Nodes++;
        
        // Track maximum ply for selective depth
        if (ply > _maxPly)
            _maxPly = ply;
        
        // Periodic progress update
        if (ply == 0 && (_info.Nodes - _lastProgressNodes) >= 1000000)
        {
            _lastProgressNodes = _info.Nodes;
            _info.Time = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
            OnSearchProgress?.Invoke(_info);
        }
        
        
        var isPvNode = beta - alpha > 1;
        if (ply < SearchConstants.MaxPly)
            _pvLength[ply] = 0;
        
        if (ply > 0 && position.IsDraw())
            return 0;
            
        // Transposition table probe
        var alphaOrig = alpha;
        Move ttMove = Move.None;
        
        if (_transpositionTable.Probe(position.ZobristKey, depth, alpha, beta, ply, out var ttScore, out ttMove))
        {
            // At root node, we need to reconstruct the full PV from TT before returning
            if (ply == 0)
            {
                if (ttMove != Move.None)
                {
                    // Reconstruct the full PV from transposition table
                    ReconstructPvFromTT(position, depth);
                }
                else
                {
                    // TT hit at root but no move stored
                    // Don't return from TT at root if we don't have a move
                    goto search_moves;
                }
            }
            return ttScore;
        }
        
        search_moves:
        
        if (depth <= 0)
        {
            // Don't enter quiescence at extreme depths
            if (ply >= 18)
                return Evaluator.Evaluate(position);
            return Quiescence(position, alpha, beta, ply);
        }
            
        if (ply >= SearchConstants.MaxPly)
            return Evaluator.Evaluate(position);
            
        CheckTimeLimit();
        
        var ourKing = GetKingSquare(position, position.SideToMove);
        var inCheck = ourKing != Square.None && MoveGenerator.IsSquareAttacked(position, 
            ourKing, 
            position.SideToMove == Color.White ? Color.Black : Color.White);
        
        // Check extension
        if (inCheck)
            depth++;
        
        var staticEval = Evaluate(position);
        
        // Null move pruning
        if (allowNull && !inCheck && ply > 0 && depth >= 3 && !isPvNode &&
            staticEval >= beta && HasNonPawnMaterial(position, position.SideToMove))
        {
            var reduction = 3 + depth / 4 + Math.Min((staticEval - beta) / 200, 3);
            var nullUndoInfo = position.MakeNullMoveWithUndo();
            
            var nullScore = -Search(position, depth - reduction - 1, -beta, -beta + 1, ply + 1, false);
            
            position.UnmakeNullMove(nullUndoInfo);
            
            if (nullScore >= beta)
            {
                // Don't trust mate scores from null move search
                if (Math.Abs(nullScore) >= SearchConstants.MateInMaxPly)
                    return beta;
                    
                // Verification search for high depths to avoid zugzwang
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
        
        // Razoring
        if (!isPvNode && !inCheck && depth <= 2 && staticEval + 300 * depth <= alpha && ply < 90)
        {
            var score = Quiescence(position, alpha, beta, ply);
            if (score <= alpha)
                return score;
        }
        
        // Futility pruning
        var futilityMargin = 0;
        if (!isPvNode && !inCheck && depth <= 3 && ply < 90)
        {
            futilityMargin = FutilityMargin(depth);
            if (staticEval + futilityMargin <= alpha)
            {
                var score = Quiescence(position, alpha, beta, ply);
                if (score <= alpha)
                    return score;
            }
        }
        
        // Reverse futility pruning (static null move pruning)
        if (!isPvNode && !inCheck && depth <= 7 && staticEval - 80 * depth >= beta)
        {
            return staticEval;
        }
        
        // Use heap allocation for deep plies to prevent stack overflow
        const int StackAllocThreshold = 15;
        Span<Move> moveBuffer = ply > StackAllocThreshold ? new Move[218] : stackalloc Move[218];
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
            var undoInfo = position.MakeMove(move);
            
            var score = 0;
            var newDepth = depth - 1;
            
            var opponentKing = GetKingSquare(position, position.SideToMove);
            var givesCheck = opponentKing != Square.None && MoveGenerator.IsSquareAttacked(position, 
                opponentKing, 
                position.SideToMove == Color.White ? Color.Black : Color.White);
            
            // Late Move Pruning (LMP)
            if (!isPvNode && !inCheck && !givesCheck && !move.IsCapture && depth <= 3 && movesSearched >= GetLMPThreshold(depth))
            {
                position.UnmakeMove(move, undoInfo);
                continue;
            }
            
            // Futility pruning for moves
            if (!isPvNode && !inCheck && !givesCheck && depth <= 3 && movesSearched > 0)
            {
                if (!move.IsCapture && !move.IsPromotion)
                {
                    if (staticEval + futilityMargin + 200 <= alpha)
                    {
                        position.UnmakeMove(move, undoInfo);
                        continue;
                    }
                }
            }
            
            // SEE pruning for bad captures at low depths
            if (!isPvNode && depth <= 2 && move.IsCapture && movesSearched > 0)
            {
                // Use simple heuristic before making the move
                var attackerValue = GetPieceValue(position.GetPiece(move.From).Type());
                var victimValue = GetPieceValue(move.CapturedPiece.Type());
                if (attackerValue > victimValue + 100)
                {
                    position.UnmakeMove(move, undoInfo);
                    continue;
                }
            }
            
            // Late move reductions (LMR)
            if (movesSearched >= 4 && depth >= 3 && !inCheck && !givesCheck &&
                !move.IsCapture && !move.IsPromotion)
            {
                var reduction = GetLMRReduction(depth, movesSearched, isPvNode);
                
                // Reduce less for moves with good history
                if (GetHistoryScore(move, position.SideToMove) > 0)
                    reduction = Math.Max(1, reduction - 1);
                    
                newDepth = Math.Max(1, newDepth - reduction);
            }
            
            // Search with reduced window for non-PV nodes
            if (movesSearched == 0)
            {
                score = -Search(position, newDepth, -beta, -alpha, ply + 1);
            }
            else
            {
                score = -Search(position, newDepth, -alpha - 1, -alpha, ply + 1);
                
                // Re-search with full window if we improve alpha
                if (score > alpha && score < beta)
                {
                    score = -Search(position, depth - 1, -beta, -alpha, ply + 1);
                }
            }
            
            movesSearched++;
            
            // Unmake the move before any early exit
            position.UnmakeMove(move, undoInfo);
            
            if (_shouldStop)
                return 0;
                
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
                
                if (score > alpha)
                {
                    alpha = score;
                    
                    if (ply < SearchConstants.MaxPly)
                        UpdatePrincipalVariation(move, ply);
                        
                    if (score >= beta)
                    {
                        if (!move.IsCapture)
                            UpdateKillerMoves(move, ply);
                        break;
                    }
                }
            }
        }
        
        // Update history for all quiet moves that were searched
        if (bestScore >= beta && !bestMove.IsCapture)
        {
            // Bonus for the move that caused cutoff
            UpdateHistoryScore(bestMove, depth * depth, position.SideToMove);
            
            // Penalty for moves that didn't cause cutoff
            for (var i = 0; i < movesSearched - 1; i++)
            {
                var move = moves[i];
                if (!move.IsCapture && move != bestMove)
                    UpdateHistoryScore(move, -depth * depth, position.SideToMove);
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
        if (ply >= SearchConstants.MaxPly)
            return Evaluator.Evaluate(position);

        if (_shouldStop)
            return 0;

        _info.Nodes++;

        var standPat = Evaluator.Evaluate(position);

        if (standPat >= beta)
            return beta;

        if (standPat > alpha)
            alpha = standPat;

        const int StackAllocThreshold = 15;
        Span<Move> moveBuffer = ply > StackAllocThreshold ? new Move[218] : stackalloc Move[218];
        var moves = new MoveList(moveBuffer);

        _moveGenerator.GenerateMoves(position, ref moves);

        Span<Move> captureBuffer = ply > StackAllocThreshold ? new Move[64] : stackalloc Move[64];
        var captures = new MoveList(captureBuffer);

        ExtractCaptures(ref moves, ref captures);

        if (captures.Count > 64)
        {
            Span<Move> limitedBuffer = ply > StackAllocThreshold ? new Move[64] : stackalloc Move[64];
            var limitedCaptures = new MoveList(limitedBuffer);
            for (int i = 0; i < Math.Min(64, captures.Count); i++)
                limitedCaptures.Add(captures[i]);
            captures = limitedCaptures;
        }

        OrderCaptures(ref captures, position);

        var bigDelta = 900;
        var inCheck = MoveGenerator.IsSquareAttacked(position,
            GetKingSquare(position, position.SideToMove),
            position.SideToMove == Color.White ? Color.Black : Color.White);

        if (!inCheck && standPat + bigDelta < alpha)
            return standPat;

        for (var i = 0; i < captures.Count; i++)
        {
            var move = captures[i];

            if ((int)move.From < 0 || (int)move.From >= 64 ||
                (int)move.To < 0 || (int)move.To >= 64)
            {
                continue;
            }

            var captureValue = GetPieceValue(move.CapturedPiece.Type());
            var promotionValue = move.IsPromotion ? GetPieceValue(move.PromotionType) - PieceValues.Pawn : 0;
            var delta = captureValue + promotionValue + 200;

            if (standPat + delta < alpha && !inCheck)
                continue;

            if (captureValue > 0 && IsLosingCapture(move, position))
                continue;

            var undoInfo = position.MakeMove(move);

            var score = -Quiescence(position, -beta, -alpha, ply + 1);

            position.UnmakeMove(move, undoInfo);

            if (_shouldStop)
                return 0;

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }
    
    private static int Evaluate(Position position)
    {
        return Evaluator.Evaluate(position);
    }
    
    private void OrderMoves(ref MoveList moves, Position position, Move ttMove, int ply)
    {
        if (moves.Count == 0)
            return;
            
        // Limit stackalloc size to prevent stack overflow
        const int maxStackSize = 218;
        Span<int> scores = moves.Count <= maxStackSize 
            ? stackalloc int[moves.Count]
            : new int[moves.Count];
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            
            if (move == ttMove)
                scores[i] = 1_000_000;
            else if (move.IsCapture)
                scores[i] = ScoreCapture(move, position) + 100_000;
            else if (IsKillerMove(move, ply))
                scores[i] = 90_000;
            else
                scores[i] = GetHistoryScore(move, position.SideToMove);
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
        if (captures.Count == 0)
            return;
            
        // Limit stackalloc size to prevent stack overflow
        const int maxStackSize = 64;
        Span<int> scores = captures.Count <= maxStackSize 
            ? stackalloc int[captures.Count]
            : new int[captures.Count];
        
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
    
    private readonly Move[,] _pvTable = new Move[SearchConstants.MaxPly, SearchConstants.MaxPly];
    private readonly int[] _pvLength = new int[SearchConstants.MaxPly];
    
    private void UpdatePrincipalVariation(Move move, int ply)
    {
        if (ply >= SearchConstants.MaxPly)
            return;
            
        _pvTable[ply, ply] = move;
        
        // Copy the rest of the PV from ply+1
        if (ply + 1 < SearchConstants.MaxPly && _pvLength[ply + 1] > 0)
        {
            var childLength = Math.Min(_pvLength[ply + 1], SearchConstants.MaxPly - ply - 1);
            for (var i = 0; i < childLength; i++)
            {
                _pvTable[ply, ply + 1 + i] = _pvTable[ply + 1, ply + 1 + i];
            }
            _pvLength[ply] = childLength + 1;
        }
        else
        {
            _pvLength[ply] = 1;
        }
        
        // Update the root PV for display
        if (ply == 0)
        {
            // Clear the queue
            while (_info.PrincipalVariation.TryDequeue(out _)) { }
            
            for (var i = 0; i < _pvLength[0]; i++)
            {
                _info.PrincipalVariation.Enqueue(_pvTable[0, i]);
            }
        }
    }
    
    private readonly Move[,] _killerMoves = new Move[SearchConstants.MaxPly, 2];
    private readonly int[,,] _historyScores = new int[2, 64, 64]; // [color, from, to]
    
    private void UpdateKillerMoves(Move move, int ply)
    {
        if (ply >= SearchConstants.MaxPly)
            return;
            
        if (_killerMoves[ply, 0] != move)
        {
            _killerMoves[ply, 1] = _killerMoves[ply, 0];
            _killerMoves[ply, 0] = move;
        }
    }
    
    private bool IsKillerMove(Move move, int ply)
    {
        return ply < SearchConstants.MaxPly && 
               (move == _killerMoves[ply, 0] || move == _killerMoves[ply, 1]);
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
        // Don't check time if searching to a specific depth
        if (_currentLimits.Depth > 0)
            return;
            
        if ((_info.Nodes & 1023) == 0)
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
    
    public int GetHashfull() => _transpositionTable.Usage();
    
    public int SelectiveDepth => _maxPly;
    
    public void ResizeTranspositionTable(int sizeMb)
    {
        if (sizeMb != _transpositionTable.SizeMb)
        {
            _transpositionTable = new TranspositionTable(sizeMb);
        }
    }
    
    public void IncrementTTAge()
    {
        _transpositionTable.NewSearch();
    }
    
    public void SetHelperThreadParameters(int threadId, int aspirationWindowAdjustment)
    {
        _threadId = threadId;
        _aspirationWindowAdjustment = aspirationWindowAdjustment;
    }
    
    public void SetTranspositionTable(TranspositionTable tt)
    {
        _transpositionTable = tt ?? throw new ArgumentNullException(nameof(tt));
    }
    
    private static bool HasNonPawnMaterial(Position position, Color color)
    {
        return position.GetBitboard(color, PieceType.Knight).IsNotEmpty() ||
               position.GetBitboard(color, PieceType.Bishop).IsNotEmpty() ||
               position.GetBitboard(color, PieceType.Rook).IsNotEmpty() ||
               position.GetBitboard(color, PieceType.Queen).IsNotEmpty();
    }
    
    private static int FutilityMargin(int depth) => depth * 100;
    
    private static int GetLMPThreshold(int depth) => depth switch
    {
        1 => 8,
        2 => 12,
        3 => 16,
        _ => int.MaxValue
    };
    
    private static int GetLMRReduction(int depth, int moveNumber, bool isPvNode)
    {
        if (depth < 3 || moveNumber < 4)
            return 0;
            
        // Base reduction using logarithmic formula
        var reduction = (int)(0.75 + Math.Log(depth) * Math.Log(moveNumber) / 2.25);
        
        // Reduce more aggressively in non-PV nodes
        if (!isPvNode)
            reduction++;
            
        // Don't reduce too much
        return Math.Min(reduction, depth - 2);
    }
    
    private void UpdateHistoryScore(Move move, int bonus, Color color)
    {
        if (move.IsCapture || move.IsPromotion)
            return;
            
        // Validate square indices
        if ((int)move.From < 0 || (int)move.From >= 64 || (int)move.To < 0 || (int)move.To >= 64)
            return;
            
        var colorIndex = color == Color.White ? 0 : 1;
        ref var score = ref _historyScores[colorIndex, (int)move.From, (int)move.To];
        
        // Improved history update with better scaling
        var absBonus = Math.Abs(bonus);
        var scaledBonus = bonus - score * absBonus / 32768;
        score += scaledBonus;
        
        // Clamp to prevent overflow
        score = Math.Clamp(score, -32768, 32768);
    }
    
    private int GetHistoryScore(Move move, Color color)
    {
        if (move.IsCapture || move.IsPromotion)
            return 0;
            
        // Validate square indices
        if ((int)move.From < 0 || (int)move.From >= 64 || (int)move.To < 0 || (int)move.To >= 64)
            return 0;
            
        var colorIndex = color == Color.White ? 0 : 1;
        return _historyScores[colorIndex, (int)move.From, (int)move.To];
    }
    
    private static bool IsLosingCapture(Move move, Position position)
    {
        try
        {
            // Validate squares first
            if ((int)move.From < 0 || (int)move.From >= 64 || 
                (int)move.To < 0 || (int)move.To >= 64)
            {
                return false; // Invalid move, don't prune
            }
            
            // Simple SEE - just check if the attacker is more valuable than the victim
            var attackerPiece = position.GetPiece(move.From);
            if (attackerPiece == Piece.None)
                return false; // No piece at from square
                
            var attackerValue = GetPieceValue(attackerPiece.Type());
            var victimValue = GetPieceValue(move.CapturedPiece.Type());
            
            // If attacker is more valuable, check if the target square is defended
            if (attackerValue > victimValue)
            {
                // Check if the target square is defended by the opponent
                var opponentColor = position.SideToMove == Color.White ? Color.Black : Color.White;
                return MoveGenerator.IsSquareAttacked(position, move.To, opponentColor);
            }
            
            return false;
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"ERROR in IsLosingCapture: {ex.Message}");
            return false; // On error, don't prune
        }
    }
    
    private void ReconstructPvFromTT(Position position, int depth)
    {
        // Clear current PV
        _pvLength[0] = 0;
        while (_info.PrincipalVariation.TryDequeue(out _)) { }
        
        // Work with a copy of the position
        var pos = new Position(position);
        var pvDepth = 0;
        
        // Allocate move buffer once outside the loop
        Span<Move> moveBuffer = stackalloc Move[218];
        
        // Try to extract PV from transposition table
        for (int d = depth; d > 0 && pvDepth < Math.Min(depth, 20); d--)
        {
            // Probe TT for this position
            if (_transpositionTable.Probe(pos.ZobristKey, d, -SearchConstants.Infinity, SearchConstants.Infinity, 0, out _, out var move))
            {
                if (move == Move.None)
                    break;
                    
                // Verify the move is legal
                var moves = new MoveList(moveBuffer);
                _moveGenerator.GenerateMoves(pos, ref moves);
                
                bool isLegal = false;
                for (int i = 0; i < moves.Count; i++)
                {
                    if (moves[i] == move)
                    {
                        isLegal = true;
                        break;
                    }
                }
                
                if (!isLegal)
                    break;
                    
                // Add move to PV
                _pvTable[0, pvDepth] = move;
                _info.PrincipalVariation.Enqueue(move);
                pvDepth++;
                
                // Make the move
                pos.MakeMove(move);
                
                // Check for repetition or draw
                if (pos.IsDraw())
                    break;
            }
            else
            {
                // No TT entry found, stop
                break;
            }
        }
        
        _pvLength[0] = pvDepth;
    }
}