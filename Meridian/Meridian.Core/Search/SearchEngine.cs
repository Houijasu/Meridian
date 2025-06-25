#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Core.Search;

public sealed class SearchEngine
{
    private readonly MoveGenerator _moveGenerator = new();
    private readonly SearchInfo _info = new();
    private Position _rootPosition = null!;
    private volatile bool _shouldStop;
    private DateTime _startTime;
    private int _allocatedTime;
    
    public SearchInfo SearchInfo => _info;
    
    public Move StartSearch(Position position, SearchLimits limits)
    {
        _rootPosition = new Position(position);
        _shouldStop = false;
        _startTime = DateTime.UtcNow;
        _allocatedTime = CalculateSearchTime(limits, position);
        
        _info.Clear();
        
        Move bestMove = Move.None;
        var maxDepth = limits.Depth > 0 ? Math.Min(limits.Depth, SearchConstants.MaxDepth) : SearchConstants.MaxDepth;
        
        for (var depth = 1; depth <= maxDepth && !_shouldStop; depth++)
        {
            var score = Search(position, depth, -SearchConstants.Infinity, SearchConstants.Infinity, 0);
            
            if (_shouldStop)
                break;
                
            bestMove = _info.PrincipalVariation.Count > 0 ? _info.PrincipalVariation[0] : Move.None;
            
            _info.Depth = depth;
            _info.Score = score;
            _info.Time = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
            
            if (Math.Abs(score) >= SearchConstants.MateScore - SearchConstants.MaxDepth)
            {
                break;
            }
            
            if (ShouldStopOnTime())
                break;
        }
        
        return bestMove;
    }
    
    public void Stop()
    {
        _shouldStop = true;
    }
    
    private int Search(Position position, int depth, int alpha, int beta, int ply)
    {
        if (_shouldStop)
            return 0;
            
        _info.Nodes++;
        
        _pvLength[ply] = ply;
        
        if (ply > 0 && position.IsDraw())
            return 0;
            
        if (depth <= 0)
            return Quiescence(position, alpha, beta, ply);
            
        if (ply >= SearchConstants.MaxDepth)
            return Evaluate(position);
            
        CheckTimeLimit();
        
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        if (moves.Count == 0)
        {
            var inCheck = MoveGenerator.IsSquareAttacked(position, 
                GetKingSquare(position), 
                position.SideToMove == Color.White ? Color.Black : Color.White);
                
            return inCheck ? -SearchConstants.MateScore + ply : 0;
        }
        
        OrderMoves(ref moves, position, Move.None, ply);
        
        Move bestMove = Move.None;
        var bestScore = -SearchConstants.Infinity;
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var newPosition = new Position(position);
            newPosition.MakeMove(move);
            
            var score = -Search(newPosition, depth - 1, -beta, -alpha, ply + 1);
            
            if (_shouldStop)
                return 0;
                
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
                        if (!move.IsCapture)
                            UpdateKillerMoves(move, ply);
                        break;
                    }
                }
            }
        }
        
        return bestScore;
    }
    
    private int Quiescence(Position position, int alpha, int beta, int ply)
    {
        if (_shouldStop)
            return 0;
            
        _info.Nodes++;
        
        if (ply >= SearchConstants.MaxDepth)
            return Evaluate(position);
        
        var standPat = Evaluate(position);
        
        if (standPat >= beta)
            return beta;
            
        if (standPat > alpha)
            alpha = standPat;
            
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        var captures = ExtractCaptures(ref moves);
        OrderCaptures(ref captures, position);
        
        for (var i = 0; i < captures.Count; i++)
        {
            var move = captures[i];
            
            // Delta pruning - skip bad captures
            var captureValue = GetPieceValue(move.CapturedPiece.Type());
            if (standPat + captureValue + 200 < alpha && move.PromotionType == PieceType.None)
                continue;
            
            var newPosition = new Position(position);
            newPosition.MakeMove(move);
            
            var score = -Quiescence(newPosition, -beta, -alpha, ply + 1);
            
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
        var score = 0;
        var us = position.SideToMove;
        var them = us == Color.White ? Color.Black : Color.White;
        
        score += CountMaterial(position, us) - CountMaterial(position, them);
        
        return score;
    }
    
    private static int CountMaterial(Position position, Color color)
    {
        var material = 0;
        
        material += position.GetBitboard(color, PieceType.Pawn).PopCount() * PieceValues.Pawn;
        material += position.GetBitboard(color, PieceType.Knight).PopCount() * PieceValues.Knight;
        material += position.GetBitboard(color, PieceType.Bishop).PopCount() * PieceValues.Bishop;
        material += position.GetBitboard(color, PieceType.Rook).PopCount() * PieceValues.Rook;
        material += position.GetBitboard(color, PieceType.Queen).PopCount() * PieceValues.Queen;
        
        return material;
    }
    
    private void OrderMoves(ref MoveList moves, Position position, Move ttMove, int ply)
    {
        Span<int> scores = stackalloc int[moves.Count];
        
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
                scores[i] = 0;
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
                captures[i] = captures[bestIndex];
                captures[bestIndex] = tempMove;
                
                var tempScore = scores[i];
                scores[i] = scores[bestIndex];
                scores[bestIndex] = tempScore;
            }
        }
    }
    
    private int ScoreCapture(Move move, Position position)
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
    
    private MoveList ExtractCaptures(ref MoveList allMoves)
    {
        Span<Move> captureBuffer = stackalloc Move[64];
        var captures = new MoveList(captureBuffer);
        
        for (var i = 0; i < allMoves.Count; i++)
        {
            var move = allMoves[i];
            if (move.IsCapture || move.IsPromotion)
                captures.Add(move);
        }
        
        return captures;
    }
    
    private readonly Move[,] _pvTable = new Move[SearchConstants.MaxDepth, SearchConstants.MaxDepth];
    private readonly int[] _pvLength = new int[SearchConstants.MaxDepth];
    
    private void UpdatePrincipalVariation(Move move, int ply)
    {
        _pvTable[ply, ply] = move;
        
        for (var i = ply + 1; i < _pvLength[ply + 1]; i++)
        {
            _pvTable[ply, i] = _pvTable[ply + 1, i];
        }
        
        _pvLength[ply] = _pvLength[ply + 1];
        
        if (ply == 0)
        {
            _info.PrincipalVariation.Clear();
            for (var i = 0; i < _pvLength[0]; i++)
            {
                _info.PrincipalVariation.Add(_pvTable[0, i]);
            }
        }
    }
    
    private readonly Move[,] _killerMoves = new Move[SearchConstants.MaxDepth, 2];
    
    private void UpdateKillerMoves(Move move, int ply)
    {
        if (ply < SearchConstants.MaxDepth)
        {
            if (_killerMoves[ply, 0] != move)
            {
                _killerMoves[ply, 1] = _killerMoves[ply, 0];
                _killerMoves[ply, 0] = move;
            }
        }
    }
    
    private bool IsKillerMove(Move move, int ply)
    {
        return ply < SearchConstants.MaxDepth && 
               (move == _killerMoves[ply, 0] || move == _killerMoves[ply, 1]);
    }
    
    private Square GetKingSquare(Position position)
    {
        var king = position.GetBitboard(position.SideToMove, PieceType.King);
        return king.IsEmpty() ? Square.None : (Square)king.GetLsbIndex();
    }
    
    private int CalculateSearchTime(SearchLimits limits, Position position)
    {
        if (limits.MoveTime > 0)
            return limits.MoveTime;
            
        if (limits.Infinite)
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
}