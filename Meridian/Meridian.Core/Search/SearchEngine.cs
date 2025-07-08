#nullable enable

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Meridian.Core.Board;
using Meridian.Core.Evaluation;
using Meridian.Core.MoveGeneration;

namespace Meridian.Core.Search;

public sealed class SearchEngine
{
    private readonly MoveGenerator _moveGenerator = new();
    private readonly SearchInfo _info = new();
    private readonly SearchData _searchData;
    private readonly int[,,] _historyScores;
    private readonly Move[,] _counterMoves;
    private TranspositionTable _transpositionTable;
    private Position _rootPosition = null!;
    private volatile bool _shouldStop;
    private DateTime _startTime;
    private int _allocatedTime;
    private SearchLimits _currentLimits = null!;
    private int _maxPly;

    public SearchInfo SearchInfo => _info;
    public Action<SearchInfo>? OnSearchProgress { get; set; }

    private readonly ThreadData? _threadData;

    public SearchEngine(TranspositionTable sharedTT, SearchData searchData, int[,,] historyScores, Move[,] counterMoves, ThreadData? threadData = null)
    {
        _transpositionTable = sharedTT ?? throw new ArgumentNullException(nameof(sharedTT));
        _searchData = searchData ?? throw new ArgumentNullException(nameof(searchData));
        _historyScores = historyScores ?? throw new ArgumentNullException(nameof(historyScores));
        _counterMoves = counterMoves ?? throw new ArgumentNullException(nameof(counterMoves));
        _threadData = threadData;
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
        _searchData.Clear();
        _transpositionTable.NewSearch();
        _maxPly = 0;

        Move bestMove = Move.None;
        var maxDepth = limits.Depth > 0 ? Math.Min(limits.Depth, SearchConstants.MaxDepth) : SearchConstants.MaxDepth;

        var aspirationDelta = 25;

        // Apply thread-specific aspiration window adjustment
        if (_threadData != null)
        {
            aspirationDelta += _threadData.AspirationWindowAdjustment;
        }

        var alpha = -SearchConstants.Infinity;
        var beta = SearchConstants.Infinity;

        for (var depth = 1; depth <= maxDepth && !_shouldStop; depth++)
        {
            var depthStartNodes = _searchData.NodeCount;
            var depthStartTime = DateTime.UtcNow;

            var score = Search(position, depth, alpha, beta, 0);

            if (!_shouldStop && (score <= alpha || score >= beta))
            {
                // Aspiration window failed, research with a full-width window
                aspirationDelta += aspirationDelta / 2; // Increase window for next time

                // Apply thread-specific aspiration window adjustment for re-search
                if (_threadData != null)
                {
                    aspirationDelta += _threadData.AspirationWindowAdjustment / 2;
                }

                alpha = -SearchConstants.Infinity;
                beta = SearchConstants.Infinity;
                score = Search(position, depth, alpha, beta, 0);
            }

            if (_shouldStop) break;

            if (_searchData.GetPvLength()[0] > 0)
            {
                bestMove = _searchData.GetPvTable()[0, 0];
            }

            _info.Depth = depth;
            _info.Score = score;
            _info.SelectiveDepth = Math.Max(_maxPly, 1);
            _info.Time = Math.Max(1, (int)(DateTime.UtcNow - _startTime).TotalMilliseconds);
            _info.Nodes = _searchData.NodeCount;
            _info.Nps = _info.Time > 0 ? (_searchData.NodeCount * 1000) / _info.Time : 0;

            // Update PV for reporting
            _info.PrincipalVariation.Clear();
            for (var i = 0; i < _searchData.GetPvLength()[0]; i++)
            {
                _info.PrincipalVariation.Enqueue(_searchData.GetPvTable()[0, i]);
            }

            OnSearchProgress?.Invoke(_info);

            if (Math.Abs(score) >= SearchConstants.MateScore - SearchConstants.MaxPly)
            {
                break;
            }

            if (_currentLimits.Depth == 0 && ShouldStopOnTime())
            {
                break;
            }

            if (depth >= 5)
            {
                alpha = score - aspirationDelta;
                beta = score + aspirationDelta;
            }
        }

        // Safety check: if no best move found, return first legal move
        if (bestMove == Move.None)
        {
            // If PV is empty, try to get a move from the transposition table
            if (_transpositionTable.Probe(_rootPosition.ZobristKey, 1, -SearchConstants.Infinity, SearchConstants.Infinity, 0, out _, out var ttMove) && ttMove != Move.None)
            {
                bestMove = ttMove;
            }
            else
            {
                Span<Move> moveBuffer = stackalloc Move[218];
                var moves = new MoveList(moveBuffer);
                _moveGenerator.GenerateMoves(_rootPosition, ref moves);
                if (moves.Count > 0)
                {
                    bestMove = moves[0];
                }
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
        if (ply >= SearchConstants.MaxPly - 1)
            return Evaluator.Evaluate(position);

        if (_shouldStop) return 0;

        _searchData.IncrementNodeCount();

        if (ply > _maxPly) _maxPly = ply;

        if (ply > 0 && position.IsDraw()) return 0;

        var isPvNode = beta - alpha > 1;
        _searchData.GetPvLength()[ply] = 0;

        var alphaOrig = alpha;
        if (_transpositionTable.Probe(position.ZobristKey, depth, alpha, beta, ply, out var ttScore, out var ttMove))
        {
            // Validate TT move before using it
            if (ttMove != Move.None && ttMove.From == ttMove.To)
            {
                ttMove = Move.None;
            }
            return ttScore;
        }

        if (depth <= 0)
        {
            return Quiescence(position, alpha, beta, ply);
        }

        CheckTimeLimit();

        var ourKing = GetKingSquare(position, position.SideToMove);
        var inCheck = ourKing != Square.None && MoveGenerator.IsSquareAttacked(position, ourKing, position.SideToMove.Opponent());

        if (inCheck) depth++;

        var staticEval = Evaluate(position);

        if (allowNull && !inCheck && ply > 0 && depth >= 3 && !isPvNode && staticEval >= beta && HasNonPawnMaterial(position, position.SideToMove))
        {
            var reduction = 3 + depth / 4 + Math.Min((staticEval - beta) / 200, 3);

            // Apply thread-specific null move reduction adjustment
            if (_threadData != null)
            {
                reduction += _threadData.NullMoveReductionAdjustment;
                reduction = Math.Max(1, reduction);
            }

            var nullUndoInfo = position.MakeNullMoveWithUndo();
            var nullScore = -Search(position, depth - reduction - 1, -beta, -beta + 1, ply + 1, false);
            position.UnmakeNullMove(nullUndoInfo);

            if (nullScore >= beta)
            {
                if (Math.Abs(nullScore) >= SearchConstants.MateInMaxPly) return beta;
                return nullScore;
            }
        }

        // Reverse Futility Pruning (also known as Static Null Move Pruning)
        // If static evaluation is far above beta, return beta (fail-high)
        if (!inCheck && !isPvNode && depth <= SearchConstants.FutilityDepthLimit &&
            staticEval - SearchConstants.ReverseFutilityMargin * depth >= beta)
        {
            return staticEval;
        }

        // Futility pruning flag - will be used in move loop
        var canUseFutilityPruning = !inCheck && !isPvNode && depth <= SearchConstants.FutilityDepthLimit &&
                                   staticEval + SearchConstants.FutilityMargin * depth <= alpha;

        Span<Move> moveBuffer = ply > 32 ? new Move[218] : stackalloc Move[218];
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

            // Track move in the search stack for counter-move heuristic
            _searchData.GetMoveStack()[ply] = move;

            var undoInfo = position.MakeMove(move);

            int score;
            var newDepth = depth - 1;

            if (movesSearched == 0)
            {
                score = -Search(position, newDepth, -beta, -alpha, ply + 1);
            }
            else
            {
                // Futility pruning - skip quiet moves that can't improve alpha
                if (canUseFutilityPruning && move.IsQuiet && !IsKillerMove(move, ply))
                {
                    movesSearched++;
                    position.UnmakeMove(move, undoInfo);
                    continue;
                }

                // SEE pruning - skip bad captures in non-PV nodes
                if (!isPvNode && move.IsCapture && depth <= 3)
                {
                    var seeScore = StaticExchangeEvaluation(position, move);
                    if (seeScore < -50) // Skip captures that lose significant material
                    {
                        movesSearched++;
                        position.UnmakeMove(move, undoInfo);
                        continue;
                    }
                }

                // Apply Late Move Reductions (LMR)
                var reduction = 0;

                // LMR conditions: not in check, not capturing, not promoting, sufficient depth and move count
                if (depth >= 3 && movesSearched >= 3 && !inCheck && move.IsQuiet)
                {
                    // Calculate reduction based on depth and move number
                    reduction = Math.Max(1, (int)(Math.Log(depth) * Math.Log(movesSearched) / 2.0));

                    // Apply thread-specific LMR adjustment
                    if (_threadData != null)
                    {
                        reduction += _threadData.LmrReductionAdjustment;
                    }

                    // Reduce less for killer moves and high history scores
                    if (IsKillerMove(move, ply) || GetHistoryScore(move, position.SideToMove) > 0)
                    {
                        reduction = Math.Max(1, reduction - 1);
                    }

                    // Don't reduce below 1
                    reduction = Math.Max(1, reduction);
                    reduction = Math.Min(reduction, newDepth - 1);
                }

                var lmrDepth = newDepth - reduction;

                score = -Search(position, lmrDepth, -alpha - 1, -alpha, ply + 1);

                // If LMR search fails high, re-search with full depth
                if (reduction > 0 && score > alpha)
                {
                    score = -Search(position, newDepth, -alpha - 1, -alpha, ply + 1);
                }

                // PVS re-search if needed
                if (score > alpha && score < beta)
                {
                    score = -Search(position, newDepth, -beta, -alpha, ply + 1);
                }
            }

            movesSearched++;
            position.UnmakeMove(move, undoInfo);

            if (_shouldStop) return 0;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;

                // Always update PV for the best move found so far
                UpdatePrincipalVariation(move, ply);

                if (score > alpha)
                {
                    alpha = score;

                    if (score >= beta)
                    {
                        if (!move.IsCapture)
                        {
                            UpdateKillerMoves(move, ply);
                            UpdateCounterMove(move, ply);
                        }
                        break;
                    }
                }
            }
        }

        if (bestScore >= beta && bestMove != Move.None && !bestMove.IsCapture)
        {
            UpdateHistoryScore(bestMove, depth * depth, position.SideToMove);
            for (var i = 0; i < movesSearched - 1; i++)
            {
                var move = moves[i];
                if (!move.IsCapture && move != bestMove)
                {
                    UpdateHistoryScore(move, -depth * depth, position.SideToMove);
                }
            }
        }

        var nodeType = bestScore <= alphaOrig ? NodeType.UpperBound : bestScore >= beta ? NodeType.LowerBound : NodeType.Exact;
        _transpositionTable.Store(position.ZobristKey, bestScore, bestMove, depth, nodeType, ply);

        return bestScore;
    }

    private int Quiescence(Position position, int alpha, int beta, int ply)
    {
        if (ply >= SearchConstants.MaxPly - 1)
            return Evaluator.Evaluate(position);

        if (_shouldStop) return 0;

        _searchData.IncrementNodeCount();

        var standPat = Evaluator.Evaluate(position);

        if (standPat >= beta) return beta;
        if (standPat > alpha) alpha = standPat;

        Span<Move> moveBuffer = ply > 32 ? new Move[218] : stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);

        // Create a new list with only captures and promotions
        Span<Move> captureBuffer = ply > 32 ? new Move[64] : stackalloc Move[64];
        var captures = new MoveList(captureBuffer);

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.IsCapture || move.IsPromotion)
            {
                captures.Add(move);
            }
        }

        OrderCaptures(ref captures, position);

        for (var i = 0; i < captures.Count; i++)
        {
            var move = captures[i];

            // Delta pruning with SEE - skip bad captures that can't improve alpha
            if (move.IsCapture)
            {
                var seeScore = StaticExchangeEvaluation(position, move);
                var deltaMargin = 200; // Additional margin for safety

                // If SEE shows we lose material and can't improve alpha, skip
                if (seeScore < 0 && standPat + seeScore + deltaMargin < alpha)
                {
                    continue; // Skip this losing capture
                }
            }

            var undoInfo = position.MakeMove(move);
            var score = -Quiescence(position, -beta, -alpha, ply + 1);
            position.UnmakeMove(move, undoInfo);

            if (_shouldStop) return 0;

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    private static int Evaluate(Position position) => Evaluator.Evaluate(position);

    private void OrderMoves(ref MoveList moves, Position position, Move ttMove, int ply)
    {
        if (moves.Count == 0) return;

        Span<int> scores = stackalloc int[218];

        // Score all moves
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move == ttMove) scores[i] = 1_000_000;
            else if (move.IsCapture) scores[i] = ScoreCapture(move, position) + 100_000;
            else if (IsKillerMove(move, ply)) scores[i] = 90_000;
            else if (IsCounterMove(move, ply)) scores[i] = 80_000;
            else scores[i] = GetHistoryScore(move, position.SideToMove);
        }

        // Use partial insertion sort - we often only need the first few moves
        // This is O(k*n) where k is the number of moves we actually search
        var sortLimit = Math.Min(moves.Count, 8); // Sort top 8 moves fully

        for (var i = 0; i < sortLimit; i++)
        {
            var bestIndex = i;
            var bestScore = scores[i];

            // Find the best move in the remaining unsorted portion
            for (var j = i + 1; j < moves.Count; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestIndex = j;
                    bestScore = scores[j];
                }
            }

            // Swap if we found a better move
            if (bestIndex != i)
            {
                var tempMove = moves[i];
                moves.Set(i, moves[bestIndex]);
                moves.Set(bestIndex, tempMove);

                var tempScore = scores[i];
                scores[i] = scores[bestIndex];
                scores[bestIndex] = tempScore;
            }
        }

        // For the remaining moves, use insertion sort as they're generated
        // This allows for incremental sorting if we need more moves
        for (var i = sortLimit; i < moves.Count && i < 32; i++)
        {
            var currentMove = moves[i];
            var currentScore = scores[i];
            var j = i - 1;

            // Skip if this move isn't good enough to be in sorted portion
            if (currentScore <= scores[sortLimit - 1])
                continue;

            // Find insertion position
            while (j >= 0 && scores[j] < currentScore)
            {
                moves.Set(j + 1, moves[j]);
                scores[j + 1] = scores[j];
                j--;
            }

            moves.Set(j + 1, currentMove);
            scores[j + 1] = currentScore;
        }
    }

    private void OrderCaptures(ref MoveList captures, Position position)
    {
        if (captures.Count == 0) return;

        Span<int> scores = stackalloc int[64];

        // Score all captures
        for (var i = 0; i < captures.Count; i++)
        {
            scores[i] = ScoreCapture(captures[i], position);
        }

        // Use partial selection sort for captures - we often prune after just a few
        var sortLimit = Math.Min(captures.Count, 6);

        for (var i = 0; i < sortLimit; i++)
        {
            var bestIndex = i;
            var bestScore = scores[i];

            for (var j = i + 1; j < captures.Count; j++)
            {
                if (scores[j] > bestScore)
                {
                    bestIndex = j;
                    bestScore = scores[j];
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
        // Use SEE for accurate capture evaluation
        var seeScore = StaticExchangeEvaluation(position, move);

        // If SEE is positive, prioritize by material gained
        if (seeScore > 0)
        {
            return seeScore + 10000; // Ensure winning captures are prioritized
        }

        // For losing/equal captures, still use basic MVV-LVA but with penalty
        var victim = GetPieceValue(move.CapturedPiece.Type());
        var attacker = GetPieceValue(position.GetPiece(move.From).Type());
        return victim * 10 - attacker + seeScore; // SEE score acts as penalty
    }

    private static int GetPieceValue(PieceType type) => type switch
    {
        PieceType.Pawn => PieceValues.Pawn,
        PieceType.Knight => PieceValues.Knight,
        PieceType.Bishop => PieceValues.Bishop,
        PieceType.Rook => PieceValues.Rook,
        PieceType.Queen => PieceValues.Queen,
        _ => 0
    };

    private void UpdatePrincipalVariation(Move move, int ply)
    {
        // Skip invalid moves
        if (move == Move.None || move.From == move.To)
        {
            return;
        }

        _searchData.GetPvTable()[ply, ply] = move;
        var childPly = ply + 1;
        if (childPly < SearchConstants.MaxPly && _searchData.GetPvLength()[childPly] > 0)
        {
            var childLength = _searchData.GetPvLength()[childPly];
            for (var i = 0; i < childLength && i < SearchConstants.MaxPly - ply - 1; i++)
            {
                var childMove = _searchData.GetPvTable()[childPly, childPly + i];
                // Skip copying invalid moves
                if (childMove != Move.None && childMove.From != childMove.To)
                {
                    _searchData.GetPvTable()[ply, ply + 1 + i] = childMove;
                }
            }
            _searchData.GetPvLength()[ply] = Math.Min(childLength + 1, SearchConstants.MaxPly - ply);
        }
        else
        {
            _searchData.GetPvLength()[ply] = 1;
        }

        if (ply == 0)
        {
            _info.PrincipalVariation.Clear();
            for (var i = 0; i < _searchData.GetPvLength()[0] && i < SearchConstants.MaxPly; i++)
            {
                var pvMove = _searchData.GetPvTable()[0, i];
                if (pvMove != Move.None && pvMove.From != pvMove.To)
                {
                    _info.PrincipalVariation.Enqueue(pvMove);
                }
            }
        }
    }

    private void UpdateKillerMoves(Move move, int ply)
    {
        if (_searchData.GetKillerMoves()[ply, 0] != move)
        {
            _searchData.GetKillerMoves()[ply, 1] = _searchData.GetKillerMoves()[ply, 0];
            _searchData.GetKillerMoves()[ply, 0] = move;
        }
    }

    private bool IsKillerMove(Move move, int ply)
    {
        return move == _searchData.GetKillerMoves()[ply, 0] || move == _searchData.GetKillerMoves()[ply, 1];
    }

    private static Square GetKingSquare(Position position, Color color)
    {
        var king = position.GetBitboard(color, PieceType.King);
        return king.IsEmpty() ? Square.None : (Square)king.GetLsbIndex();
    }

    private static int CalculateSearchTime(SearchLimits limits, Position position)
    {
        if (limits.MoveTime > 0) return limits.MoveTime;
        if (limits.Infinite || limits.Depth > 0) return int.MaxValue;

        var timeLeft = position.SideToMove == Color.White ? limits.WhiteTime : limits.BlackTime;
        var increment = position.SideToMove == Color.White ? limits.WhiteIncrement : limits.BlackIncrement;

        if (timeLeft <= 0) return 100;

        var movesToGo = limits.MovesToGo > 0 ? limits.MovesToGo : 40;
        var baseTime = timeLeft / movesToGo;
        var totalTime = baseTime + increment * 3 / 4;

        return Math.Min(totalTime, timeLeft - 50);
    }

    private void CheckTimeLimit()
    {
        if (_currentLimits.Depth > 0) return;
        if ((_info.Nodes & 1023) == 0 && ShouldStopOnTime())
        {
            _shouldStop = true;
        }
    }

    private bool ShouldStopOnTime()
    {
        var elapsed = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
        return elapsed >= _allocatedTime;
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

    private void UpdateHistoryScore(Move move, int bonus, Color color)
    {
        if (move.IsCapture || move.IsPromotion) return;
        if ((int)move.From < 0 || (int)move.From >= 64 || (int)move.To < 0 || (int)move.To >= 64) return;

        var colorIndex = color == Color.White ? 0 : 1;

        // Apply thread-specific history score multiplier
        if (_threadData != null)
        {
            bonus = (int)(bonus * _threadData.HistoryScoreMultiplier);
        }

        Interlocked.Add(ref _historyScores[colorIndex, (int)move.From, (int)move.To], bonus);
    }

    private int GetHistoryScore(Move move, Color color)
    {
        if (move.IsCapture || move.IsPromotion) return 0;
        if ((int)move.From < 0 || (int)move.From >= 64 || (int)move.To < 0 || (int)move.To >= 64) return 0;

        var colorIndex = color == Color.White ? 0 : 1;
        var score = _historyScores[colorIndex, (int)move.From, (int)move.To];

        // Apply thread-specific history score multiplier
        if (_threadData != null)
        {
            score = (int)(score * _threadData.HistoryScoreMultiplier);
        }

        return score;
    }

    private void UpdateCounterMove(Move move, int ply)
    {
        if (ply <= 0) return;

        // Get the previous move from the search stack
        var prevMove = _searchData.GetMoveStack()[ply - 1];
        if (prevMove == Move.None || (int)prevMove.From >= 64 || (int)prevMove.To >= 64) return;

        // Store this move as a counter to the previous move (thread-safe)
        lock (_counterMoves)
        {
            _counterMoves[(int)prevMove.From, (int)prevMove.To] = move;
        }
    }

    private bool IsCounterMove(Move move, int ply)
    {
        if (ply <= 0) return false;

        // Get the previous move from the search stack
        var prevMove = _searchData.GetMoveStack()[ply - 1];
        if (prevMove == Move.None || (int)prevMove.From >= 64 || (int)prevMove.To >= 64) return false;

        // Check if this move is the stored counter-move (thread-safe read)
        lock (_counterMoves)
        {
            return _counterMoves[(int)prevMove.From, (int)prevMove.To] == move;
        }
    }

    private static int StaticExchangeEvaluation(Position position, Move move)
    {
        if (!move.IsCapture) return 0;

        var target = move.To;
        var attackers = GetAttackersToSquare(position, target);
        var defenders = GetDefendersToSquare(position, target, position.SideToMove.Opponent());

        var gain = new int[32]; // Maximum reasonable capture depth
        var depth = 0;
        var capturedValue = GetPieceValue(move.CapturedPiece.Type());
        gain[depth] = capturedValue;

        var fromPiece = position.GetPiece(move.From);
        var attackingValue = GetPieceValue(fromPiece.Type());

        // Simulate the exchange
        var currentSide = position.SideToMove.Opponent(); // Side to move after initial capture
        var occupancy = (ulong)position.OccupiedSquares();
        occupancy &= ~(1UL << (int)move.From); // Remove the initial attacker

        depth++;
        while (depth < 31)
        {
            // Find least valuable attacker for current side
            var nextAttacker = GetLeastValuableAttacker(position, target, currentSide, occupancy);
            if (nextAttacker.square == Square.None) break;

            // Make the capture
            gain[depth] = attackingValue - gain[depth - 1];
            attackingValue = GetPieceValue(nextAttacker.pieceType);

            // Remove the attacker from occupancy
            occupancy &= ~(1UL << (int)nextAttacker.square);

            // Update attacking pieces that may now have a clear path
            UpdateXrayAttackers(position, target, nextAttacker.square, ref occupancy);

            currentSide = currentSide.Opponent();
            depth++;
        }

        // Minimax backwards through the gain array
        while (--depth > 0)
        {
            gain[depth - 1] = Math.Max(-gain[depth], gain[depth - 1]);
        }

        return gain[0];
    }

    private static ulong GetAttackersToSquare(Position position, Square square)
    {
        var occupancy = (ulong)position.OccupiedSquares();
        var attackers = 0UL;

        // Pawn attacks
        var targetBit = 1UL << (int)square;
        var whitePawns = position.GetBitboard(Color.White, PieceType.Pawn);
        var blackPawns = position.GetBitboard(Color.Black, PieceType.Pawn);

        // White pawn attacks (from rank below)
        if ((int)square >= 8)
        {
            if (((int)square & 7) > 0 && ((ulong)whitePawns & (1UL << ((int)square - 9))) != 0)
                attackers |= 1UL << ((int)square - 9);
            if (((int)square & 7) < 7 && ((ulong)whitePawns & (1UL << ((int)square - 7))) != 0)
                attackers |= 1UL << ((int)square - 7);
        }

        // Black pawn attacks (from rank above)
        if ((int)square < 56)
        {
            if (((int)square & 7) > 0 && ((ulong)blackPawns & (1UL << ((int)square + 7))) != 0)
                attackers |= 1UL << ((int)square + 7);
            if (((int)square & 7) < 7 && ((ulong)blackPawns & (1UL << ((int)square + 9))) != 0)
                attackers |= 1UL << ((int)square + 9);
        }

        // Knight attacks
        var knights = (ulong)(position.GetBitboard(Color.White, PieceType.Knight) |
                     position.GetBitboard(Color.Black, PieceType.Knight));
        var knightAttacks = GetKnightAttacks(square);
        attackers |= knights & knightAttacks;

        // Bishop/Queen diagonal attacks
        var bishops = (ulong)(position.GetBitboard(Color.White, PieceType.Bishop) |
                     position.GetBitboard(Color.Black, PieceType.Bishop) |
                     position.GetBitboard(Color.White, PieceType.Queen) |
                     position.GetBitboard(Color.Black, PieceType.Queen));
        var bishopAttacks = GetBishopAttacks(square, occupancy);
        attackers |= bishops & bishopAttacks;

        // Rook/Queen straight attacks
        var rooks = (ulong)(position.GetBitboard(Color.White, PieceType.Rook) |
                   position.GetBitboard(Color.Black, PieceType.Rook) |
                   position.GetBitboard(Color.White, PieceType.Queen) |
                   position.GetBitboard(Color.Black, PieceType.Queen));
        var rookAttacks = GetRookAttacks(square, occupancy);
        attackers |= rooks & rookAttacks;

        // King attacks
        var kings = (ulong)(position.GetBitboard(Color.White, PieceType.King) |
                   position.GetBitboard(Color.Black, PieceType.King));
        var kingAttacks = GetKingAttacks(square);
        attackers |= kings & kingAttacks;

        return attackers;
    }

    private static ulong GetDefendersToSquare(Position position, Square square, Color defendingColor)
    {
        var attackers = GetAttackersToSquare(position, square);

        // Filter by defending color
        var colorPieces = (ulong)(position.GetBitboard(defendingColor, PieceType.Pawn) |
                         position.GetBitboard(defendingColor, PieceType.Knight) |
                         position.GetBitboard(defendingColor, PieceType.Bishop) |
                         position.GetBitboard(defendingColor, PieceType.Rook) |
                         position.GetBitboard(defendingColor, PieceType.Queen) |
                         position.GetBitboard(defendingColor, PieceType.King));

        return attackers & colorPieces;
    }

    private static (Square square, PieceType pieceType) GetLeastValuableAttacker(Position position, Square target, Color color, ulong occupancy)
    {
        // Try pieces in order of value (least valuable first)
        var colorPieces = (ulong)(position.GetBitboard(color, PieceType.Pawn) |
                         position.GetBitboard(color, PieceType.Knight) |
                         position.GetBitboard(color, PieceType.Bishop) |
                         position.GetBitboard(color, PieceType.Rook) |
                         position.GetBitboard(color, PieceType.Queen) |
                         position.GetBitboard(color, PieceType.King));

        colorPieces &= occupancy; // Only consider pieces still on board

        // Pawn
        var pawns = (ulong)position.GetBitboard(color, PieceType.Pawn) & occupancy;
        if (pawns != 0)
        {
            var pawnAttackers = GetPawnAttackers(target, color) & pawns;
            if (pawnAttackers != 0)
            {
                var square = (Square)BitOperations.TrailingZeroCount(pawnAttackers);
                return (square, PieceType.Pawn);
            }
        }

        // Knight
        var knights = (ulong)position.GetBitboard(color, PieceType.Knight) & occupancy;
        if (knights != 0)
        {
            var knightAttackers = GetKnightAttacks(target) & knights;
            if (knightAttackers != 0)
            {
                var square = (Square)BitOperations.TrailingZeroCount(knightAttackers);
                return (square, PieceType.Knight);
            }
        }

        // Bishop
        var bishops = (ulong)position.GetBitboard(color, PieceType.Bishop) & occupancy;
        if (bishops != 0)
        {
            var bishopAttackers = GetBishopAttacks(target, occupancy) & bishops;
            if (bishopAttackers != 0)
            {
                var square = (Square)BitOperations.TrailingZeroCount(bishopAttackers);
                return (square, PieceType.Bishop);
            }
        }

        // Rook
        var rooks = (ulong)position.GetBitboard(color, PieceType.Rook) & occupancy;
        if (rooks != 0)
        {
            var rookAttackers = GetRookAttacks(target, occupancy) & rooks;
            if (rookAttackers != 0)
            {
                var square = (Square)BitOperations.TrailingZeroCount(rookAttackers);
                return (square, PieceType.Rook);
            }
        }

        // Queen
        var queens = (ulong)position.GetBitboard(color, PieceType.Queen) & occupancy;
        if (queens != 0)
        {
            var queenAttackers = (GetBishopAttacks(target, occupancy) | GetRookAttacks(target, occupancy)) & queens;
            if (queenAttackers != 0)
            {
                var square = (Square)BitOperations.TrailingZeroCount(queenAttackers);
                return (square, PieceType.Queen);
            }
        }

        // King
        var kings = (ulong)position.GetBitboard(color, PieceType.King) & occupancy;
        if (kings != 0)
        {
            var kingAttackers = GetKingAttacks(target) & kings;
            if (kingAttackers != 0)
            {
                var square = (Square)BitOperations.TrailingZeroCount(kingAttackers);
                return (square, PieceType.King);
            }
        }

        return (Square.None, PieceType.None);
    }

    private static ulong GetPawnAttackers(Square target, Color color)
    {
        var targetBit = 1UL << (int)target;
        var attacks = 0UL;

        if (color == Color.White)
        {
            // White pawns attack diagonally upward
            if ((int)target >= 8)
            {
                if (((int)target & 7) > 0) attacks |= 1UL << ((int)target - 9);
                if (((int)target & 7) < 7) attacks |= 1UL << ((int)target - 7);
            }
        }
        else
        {
            // Black pawns attack diagonally downward
            if ((int)target < 56)
            {
                if (((int)target & 7) > 0) attacks |= 1UL << ((int)target + 7);
                if (((int)target & 7) < 7) attacks |= 1UL << ((int)target + 9);
            }
        }

        return attacks;
    }

    private static ulong GetKnightAttacks(Square square)
    {
        var attacks = 0UL;
        var sq = (int)square;
        var rank = sq / 8;
        var file = sq % 8;

        var knightMoves = new int[]
        {
            -17, -15, -10, -6, 6, 10, 15, 17
        };

        foreach (var move in knightMoves)
        {
            var newSq = sq + move;
            if (newSq >= 0 && newSq < 64)
            {
                var newRank = newSq / 8;
                var newFile = newSq % 8;

                // Check if the move is valid (not wrapping around the board)
                if (Math.Abs(newRank - rank) <= 2 && Math.Abs(newFile - file) <= 2)
                {
                    attacks |= 1UL << newSq;
                }
            }
        }

        return attacks;
    }

    private static ulong GetKingAttacks(Square square)
    {
        var attacks = 0UL;
        var sq = (int)square;
        var rank = sq / 8;
        var file = sq % 8;

        for (int dr = -1; dr <= 1; dr++)
        {
            for (int df = -1; df <= 1; df++)
            {
                if (dr == 0 && df == 0) continue;

                var newRank = rank + dr;
                var newFile = file + df;

                if (newRank >= 0 && newRank < 8 && newFile >= 0 && newFile < 8)
                {
                    attacks |= 1UL << (newRank * 8 + newFile);
                }
            }
        }

        return attacks;
    }

    private static ulong GetBishopAttacks(Square square, ulong occupancy)
    {
        // Simplified bishop attack generation - would use magic bitboards in production
        var attacks = 0UL;
        var sq = (int)square;

        // Diagonal directions: NE, NW, SE, SW
        var directions = new int[] { 9, 7, -7, -9 };

        foreach (var dir in directions)
        {
            for (int i = 1; i < 8; i++)
            {
                var newSq = sq + dir * i;
                if (newSq < 0 || newSq >= 64) break;

                // Check for board wrap
                var oldFile = (sq + dir * (i - 1)) % 8;
                var newFile = newSq % 8;
                if (Math.Abs(newFile - oldFile) != 1) break;

                attacks |= 1UL << newSq;

                if ((occupancy & (1UL << newSq)) != 0) break; // Blocked
            }
        }

        return attacks;
    }

    private static ulong GetRookAttacks(Square square, ulong occupancy)
    {
        // Simplified rook attack generation - would use magic bitboards in production
        var attacks = 0UL;
        var sq = (int)square;
        var rank = sq / 8;
        var file = sq % 8;

        // Horizontal attacks (same rank)
        for (int f = file + 1; f < 8; f++)
        {
            var targetSq = rank * 8 + f;
            attacks |= 1UL << targetSq;
            if ((occupancy & (1UL << targetSq)) != 0) break;
        }

        for (int f = file - 1; f >= 0; f--)
        {
            var targetSq = rank * 8 + f;
            attacks |= 1UL << targetSq;
            if ((occupancy & (1UL << targetSq)) != 0) break;
        }

        // Vertical attacks (same file)
        for (int r = rank + 1; r < 8; r++)
        {
            var targetSq = r * 8 + file;
            attacks |= 1UL << targetSq;
            if ((occupancy & (1UL << targetSq)) != 0) break;
        }

        for (int r = rank - 1; r >= 0; r--)
        {
            var targetSq = r * 8 + file;
            attacks |= 1UL << targetSq;
            if ((occupancy & (1UL << targetSq)) != 0) break;
        }

        return attacks;
    }

    private static void UpdateXrayAttackers(Position position, Square target, Square removedPiece, ref ulong occupancy)
    {
        // When a piece is removed, it might uncover x-ray attacks
        // This is a simplified version - in production, you'd update specific sliding piece attacks

        // For now, we don't handle x-ray attacks in this basic implementation
        // This could be enhanced later for more accuracy
    }
}
