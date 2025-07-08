#nullable enable

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
    private TranspositionTable _transpositionTable;
    private Position _rootPosition = null!;
    private volatile bool _shouldStop;
    private DateTime _startTime;
    private int _allocatedTime;
    private SearchLimits _currentLimits = null!;
    private int _maxPly;

    public SearchInfo SearchInfo => _info;
    public Action<SearchInfo>? OnSearchProgress { get; set; }

    public SearchEngine(TranspositionTable sharedTT, SearchData searchData, int[,,] historyScores)
    {
        _transpositionTable = sharedTT ?? throw new ArgumentNullException(nameof(sharedTT));
        _searchData = searchData ?? throw new ArgumentNullException(nameof(searchData));
        _historyScores = historyScores ?? throw new ArgumentNullException(nameof(historyScores));
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
            var nullUndoInfo = position.MakeNullMoveWithUndo();
            var nullScore = -Search(position, depth - reduction - 1, -beta, -beta + 1, ply + 1, false);
            position.UnmakeNullMove(nullUndoInfo);

            if (nullScore >= beta)
            {
                if (Math.Abs(nullScore) >= SearchConstants.MateInMaxPly) return beta;
                return nullScore;
            }
        }

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
            var undoInfo = position.MakeMove(move);

            int score;
            var newDepth = depth - 1;

            if (movesSearched == 0)
            {
                score = -Search(position, newDepth, -beta, -alpha, ply + 1);
            }
            else
            {
                score = -Search(position, newDepth, -alpha - 1, -alpha, ply + 1);
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

                if (score > alpha)
                {
                    alpha = score;
                    UpdatePrincipalVariation(move, ply);

                    if (score >= beta)
                    {
                        if (!move.IsCapture) UpdateKillerMoves(move, ply);
                        break;
                    }
                }
                else if (ply == 0)
                {
                    // At root, always update PV with the best move found so far
                    UpdatePrincipalVariation(move, ply);
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
        var victim = GetPieceValue(move.CapturedPiece.Type());
        var attacker = GetPieceValue(position.GetPiece(move.From).Type());
        return victim * 10 - attacker;
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
        Interlocked.Add(ref _historyScores[colorIndex, (int)move.From, (int)move.To], bonus);
    }

    private int GetHistoryScore(Move move, Color color)
    {
        if (move.IsCapture || move.IsPromotion) return 0;
        if ((int)move.From < 0 || (int)move.From >= 64 || (int)move.To < 0 || (int)move.To >= 64) return 0;

        var colorIndex = color == Color.White ? 0 : 1;
        return _historyScores[colorIndex, (int)move.From, (int)move.To];
    }
}
