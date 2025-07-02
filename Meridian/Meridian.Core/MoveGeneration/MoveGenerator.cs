#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;

namespace Meridian.Core.MoveGeneration;

public sealed class MoveGenerator
{
    private Position _position = null!;
    private Bitboard _checkers;
    private Bitboard _pinned;
    private Bitboard _checkMask;
    private bool _inCheck;
    private bool _inDoubleCheck;
    private readonly Bitboard[] _pinRays = new Bitboard[64];
    
    public unsafe void GenerateMoves(Position position, ref MoveList moves)
    {
        _position = position;
        moves.Clear();
        
        // Generate pseudo-legal moves first
        Span<Move> pseudoLegalBuffer = stackalloc Move[218];
        var pseudoLegalMoves = new MoveList(pseudoLegalBuffer);
        
        CalculateCheckersAndPinned();
        
        if (_inDoubleCheck)
        {
            GenerateKingMoves(ref pseudoLegalMoves);
        }
        else
        {
            GeneratePawnMoves(ref pseudoLegalMoves);
            GenerateKnightMoves(ref pseudoLegalMoves);
            GenerateBishopMoves(ref pseudoLegalMoves);
            GenerateRookMoves(ref pseudoLegalMoves);
            GenerateQueenMoves(ref pseudoLegalMoves);
            GenerateKingMoves(ref pseudoLegalMoves);
        }
        
        // Filter to only legal moves
        for (var i = 0; i < pseudoLegalMoves.Count; i++)
        {
            var move = pseudoLegalMoves[i];
            if (IsMoveLegal(move))
            {
                moves.Add(move);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bitboard GetAttackers(Position position, Square square, Bitboard occupancy)
    {
        ArgumentNullException.ThrowIfNull(position);
        
        var attackers = Bitboard.Empty;
        
        attackers |= AttackTables.PawnAttacks(square, Color.White) & position.GetBitboard(Color.Black, PieceType.Pawn);
        attackers |= AttackTables.PawnAttacks(square, Color.Black) & position.GetBitboard(Color.White, PieceType.Pawn);
        
        attackers |= AttackTables.KnightAttacks(square) & 
                    (position.GetBitboard(PieceType.Knight));
                    
        attackers |= AttackTables.KingAttacks(square) & 
                    (position.GetBitboard(PieceType.King));
                    
        attackers |= MagicBitboards.GetBishopAttacks(square, occupancy) & 
                    (position.GetBitboard(PieceType.Bishop) | position.GetBitboard(PieceType.Queen));
                    
        attackers |= MagicBitboards.GetRookAttacks(square, occupancy) & 
                    (position.GetBitboard(PieceType.Rook) | position.GetBitboard(PieceType.Queen));
                    
        return attackers;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSquareAttacked(Position position, Square square, Color byColor)
    {
        ArgumentNullException.ThrowIfNull(position);
        
        var occupancy = position.OccupiedSquares();
        var enemyPieces = position.GetBitboard(byColor);
        
        if ((AttackTables.PawnAttacks(square, byColor == Color.White ? Color.Black : Color.White) & 
             position.GetBitboard(byColor, PieceType.Pawn)).IsNotEmpty())
            return true;
            
        if ((AttackTables.KnightAttacks(square) & 
             position.GetBitboard(byColor, PieceType.Knight)).IsNotEmpty())
            return true;
            
        if ((AttackTables.KingAttacks(square) & 
             position.GetBitboard(byColor, PieceType.King)).IsNotEmpty())
            return true;
            
        var bishopQueens = position.GetBitboard(byColor, PieceType.Bishop) | 
                           position.GetBitboard(byColor, PieceType.Queen);
        if ((MagicBitboards.GetBishopAttacks(square, occupancy) & bishopQueens).IsNotEmpty())
            return true;
            
        var rookQueens = position.GetBitboard(byColor, PieceType.Rook) | 
                         position.GetBitboard(byColor, PieceType.Queen);
        if ((MagicBitboards.GetRookAttacks(square, occupancy) & rookQueens).IsNotEmpty())
            return true;
            
        return false;
    }
    
    private void CalculateCheckersAndPinned()
    {
        Array.Clear(_pinRays);
        
        var us = _position.SideToMove;
        var them = us == Color.White ? Color.Black : Color.White;
        var king = _position.GetBitboard(us, PieceType.King);
        
        if (king.IsEmpty())
        {
            _checkers = Bitboard.Empty;
            _pinned = Bitboard.Empty;
            _checkMask = Bitboard.Full;
            _inCheck = false;
            _inDoubleCheck = false;
            return;
        }
        
        var kingSquare = (Square)king.GetLsbIndex();
        var occupancy = _position.OccupiedSquares();
        
        _checkers = GetAttackers(_position, kingSquare, occupancy) & _position.GetBitboard(them);
        _inCheck = _checkers.IsNotEmpty();
        _inDoubleCheck = Bitboard.PopCount(_checkers) > 1;
        
        _pinned = Bitboard.Empty;
        _checkMask = _inCheck ? Bitboard.Empty : Bitboard.Full;
        
        if (_inCheck && !_inDoubleCheck)
        {
            var checker = (Square)_checkers.GetLsbIndex();
            _checkMask = _checkers | GetRayBetween(kingSquare, checker);
        }
        
        var enemyBishopQueens = _position.GetBitboard(them, PieceType.Bishop) | 
                                _position.GetBitboard(them, PieceType.Queen);
        var enemyRookQueens = _position.GetBitboard(them, PieceType.Rook) | 
                              _position.GetBitboard(them, PieceType.Queen);
        
        var potentialPinners = (MagicBitboards.GetBishopAttacks(kingSquare, Bitboard.Empty) & enemyBishopQueens) |
                              (MagicBitboards.GetRookAttacks(kingSquare, Bitboard.Empty) & enemyRookQueens);
        
        while (potentialPinners.IsNotEmpty())
        {
            var pinner = (Square)potentialPinners.GetLsbIndex();
            var between = GetRayBetween(kingSquare, pinner) & occupancy;
            
            if (Bitboard.PopCount(between) == 1)
            {
                _pinned |= between;
                var pinnedSquare = between.GetLsbIndex();
                _pinRays[pinnedSquare] = GetRayBetween(kingSquare, pinner) | kingSquare.ToBitboard() | pinner.ToBitboard();
            }
            
            potentialPinners = potentialPinners.RemoveLsb();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetRayBetween(Square from, Square to)
    {
        var fileDiff = to.File() - from.File();
        var rankDiff = to.Rank() - from.Rank();
        
        if (fileDiff == 0 && rankDiff == 0) return Bitboard.Empty;
        
        // Ensure the squares are on the same line (rank, file, or diagonal)
        if (fileDiff != 0 && rankDiff != 0 && 
            Math.Abs(fileDiff) != Math.Abs(rankDiff))
            return Bitboard.Empty;
        
        int direction;
        if (fileDiff == 0)
            direction = rankDiff > 0 ? AttackTables.Directions.North : AttackTables.Directions.South;
        else if (rankDiff == 0)
            direction = fileDiff > 0 ? AttackTables.Directions.East : AttackTables.Directions.West;
        else if (fileDiff == rankDiff)
            direction = fileDiff > 0 ? AttackTables.Directions.NorthEast : AttackTables.Directions.SouthWest;
        else // fileDiff == -rankDiff
            direction = fileDiff > 0 ? AttackTables.Directions.SouthEast : AttackTables.Directions.NorthWest;
            
        var ray1 = AttackTables.GetRay(from, direction);
        
        // Get opposite direction
        var oppositeDirection = direction switch
        {
            AttackTables.Directions.NorthWest => AttackTables.Directions.SouthEast,
            AttackTables.Directions.North => AttackTables.Directions.South,
            AttackTables.Directions.NorthEast => AttackTables.Directions.SouthWest,
            AttackTables.Directions.West => AttackTables.Directions.East,
            AttackTables.Directions.East => AttackTables.Directions.West,
            AttackTables.Directions.SouthWest => AttackTables.Directions.NorthEast,
            AttackTables.Directions.South => AttackTables.Directions.North,
            AttackTables.Directions.SouthEast => AttackTables.Directions.NorthWest,
            _ => direction
        };
        
        var ray2 = AttackTables.GetRay(to, oppositeDirection);
        
        return ray1 & ray2;
    }
    
    private void GeneratePawnMoves(ref MoveList moves)
    {
        var us = _position.SideToMove;
        var them = us == Color.White ? Color.Black : Color.White;
        var pawns = _position.GetBitboard(us, PieceType.Pawn);
        var empty = _position.EmptySquares();
        var enemies = _position.GetBitboard(them);
        
        var rank7 = us == Color.White ? AttackTables.Rank7 : AttackTables.Rank2;
        var rank3 = us == Color.White ? AttackTables.Rank3 : AttackTables.Rank6;
        var pushDirection = us == Color.White ? 8 : -8;
        var captureLeftDirection = us == Color.White ? 7 : -9;
        var captureRightDirection = us == Color.White ? 9 : -7;
        
        var promotionPawns = pawns & rank7;
        var nonPromotionPawns = pawns & ~rank7;
        
        if (nonPromotionPawns.IsNotEmpty())
        {
            var singlePushes = us == Color.White ? 
                (nonPromotionPawns << 8) & empty :
                (nonPromotionPawns >> 8) & empty;
                
            var doublePushes = us == Color.White ?
                ((singlePushes & rank3) << 8) & empty :
                ((singlePushes & rank3) >> 8) & empty;
                
            GeneratePawnPushes(singlePushes, pushDirection, MoveType.None, ref moves);
            GeneratePawnPushes(doublePushes, pushDirection * 2, MoveType.DoublePush, ref moves);
            
            var leftCaptures = us == Color.White ?
                ((nonPromotionPawns & AttackTables.NotFileA) << 7) & enemies :
                ((nonPromotionPawns & AttackTables.NotFileH) >> 9) & enemies;
                
            var rightCaptures = us == Color.White ?
                ((nonPromotionPawns & AttackTables.NotFileH) << 9) & enemies :
                ((nonPromotionPawns & AttackTables.NotFileA) >> 7) & enemies;
                
            GeneratePawnCaptures(leftCaptures, captureLeftDirection, ref moves);
            GeneratePawnCaptures(rightCaptures, captureRightDirection, ref moves);
            
            if (_position.EnPassantSquare != Square.None)
            {
                var epTarget = _position.EnPassantSquare.ToBitboard();
                if ((AttackTables.PawnAttacks(_position.EnPassantSquare, them) & nonPromotionPawns).IsNotEmpty())
                {
                    var attackers = AttackTables.PawnAttacks(_position.EnPassantSquare, them) & nonPromotionPawns;
                    while (attackers.IsNotEmpty())
                    {
                        var from = (Square)attackers.GetLsbIndex();
                        moves.Add(from, _position.EnPassantSquare, 
                                  MoveType.Capture | MoveType.EnPassant, 
                                  PieceExtensions.MakePiece(them, PieceType.Pawn));
                        attackers = attackers.RemoveLsb();
                    }
                }
            }
        }
        
        if (promotionPawns.IsNotEmpty())
        {
            var promoPushes = us == Color.White ?
                (promotionPawns << 8) & empty :
                (promotionPawns >> 8) & empty;
                
            GeneratePromotions(promoPushes, pushDirection, MoveType.None, ref moves);
            
            var promoLeftCaptures = us == Color.White ?
                ((promotionPawns & AttackTables.NotFileA) << 7) & enemies :
                ((promotionPawns & AttackTables.NotFileH) >> 9) & enemies;
                
            var promoRightCaptures = us == Color.White ?
                ((promotionPawns & AttackTables.NotFileH) << 9) & enemies :
                ((promotionPawns & AttackTables.NotFileA) >> 7) & enemies;
                
            GeneratePromotionCaptures(promoLeftCaptures, captureLeftDirection, ref moves);
            GeneratePromotionCaptures(promoRightCaptures, captureRightDirection, ref moves);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GeneratePawnPushes(Bitboard pushes, int direction, MoveType flags, ref MoveList moves)
    {
        pushes &= _checkMask;
        while (pushes.IsNotEmpty())
        {
            var to = (Square)pushes.GetLsbIndex();
            var from = (Square)((int)to - direction);
            
            if ((_pinned & from.ToBitboard()).IsEmpty() || 
                IsMoveLegalWhenPinned(from, to))
            {
                moves.Add(from, to, flags);
            }
            
            pushes = pushes.RemoveLsb();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GeneratePawnCaptures(Bitboard captures, int direction, ref MoveList moves)
    {
        captures &= _checkMask;
        while (captures.IsNotEmpty())
        {
            var to = (Square)captures.GetLsbIndex();
            var from = (Square)((int)to - direction);
            var captured = _position.GetPiece(to);
            
            if ((_pinned & from.ToBitboard()).IsEmpty() || 
                IsMoveLegalWhenPinned(from, to))
            {
                moves.Add(from, to, MoveType.Capture, captured);
            }
            
            captures = captures.RemoveLsb();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GeneratePromotions(Bitboard pushes, int direction, MoveType baseFlags, ref MoveList moves)
    {
        pushes &= _checkMask;
        while (pushes.IsNotEmpty())
        {
            var to = (Square)pushes.GetLsbIndex();
            var from = (Square)((int)to - direction);
            
            if ((_pinned & from.ToBitboard()).IsEmpty() || 
                IsMoveLegalWhenPinned(from, to))
            {
                moves.AddPromotions(from, to, baseFlags);
            }
            
            pushes = pushes.RemoveLsb();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GeneratePromotionCaptures(Bitboard captures, int direction, ref MoveList moves)
    {
        captures &= _checkMask;
        while (captures.IsNotEmpty())
        {
            var to = (Square)captures.GetLsbIndex();
            var from = (Square)((int)to - direction);
            var captured = _position.GetPiece(to);
            
            if ((_pinned & from.ToBitboard()).IsEmpty() || 
                IsMoveLegalWhenPinned(from, to))
            {
                moves.AddPromotions(from, to, MoveType.Capture, captured);
            }
            
            captures = captures.RemoveLsb();
        }
    }
    
    private void GenerateKnightMoves(ref MoveList moves)
    {
        var us = _position.SideToMove;
        var knights = _position.GetBitboard(us, PieceType.Knight) & ~_pinned;
        var targets = ~_position.GetBitboard(us) & _checkMask;
        
        while (knights.IsNotEmpty())
        {
            var from = (Square)knights.GetLsbIndex();
            var attacks = AttackTables.KnightAttacks(from) & targets;
            
            while (attacks.IsNotEmpty())
            {
                var to = (Square)attacks.GetLsbIndex();
                var captured = _position.GetPiece(to);
                
                if (captured == Piece.None)
                    moves.AddQuiet(from, to);
                else
                    moves.AddCapture(from, to, captured);
                    
                attacks = attacks.RemoveLsb();
            }
            
            knights = knights.RemoveLsb();
        }
    }
    
    private void GenerateBishopMoves(ref MoveList moves)
    {
        GenerateSlidingMoves(PieceType.Bishop, MagicBitboards.GetBishopAttacks, ref moves);
    }
    
    private void GenerateRookMoves(ref MoveList moves)
    {
        GenerateSlidingMoves(PieceType.Rook, MagicBitboards.GetRookAttacks, ref moves);
    }
    
    private void GenerateQueenMoves(ref MoveList moves)
    {
        GenerateSlidingMoves(PieceType.Queen, MagicBitboards.GetQueenAttacks, ref moves);
    }
    
    private void GenerateSlidingMoves(PieceType pieceType, 
        Func<Square, Bitboard, Bitboard> getAttacks, ref MoveList moves)
    {
        var us = _position.SideToMove;
        var pieces = _position.GetBitboard(us, pieceType);
        var occupancy = _position.OccupiedSquares();
        var targets = ~_position.GetBitboard(us) & _checkMask;
        
        while (pieces.IsNotEmpty())
        {
            var from = (Square)pieces.GetLsbIndex();
            var isPinned = (_pinned & from.ToBitboard()).IsNotEmpty();
            var attacks = getAttacks(from, occupancy) & targets;
            
            if (isPinned)
            {
                var king = (Square)_position.GetBitboard(us, PieceType.King).GetLsbIndex();
                // Include both the ray between king and piece AND the extension beyond the piece
                // This allows the pinned piece to capture its pinner
                var fullPinRay = GetFullRay(king, from);
                attacks &= fullPinRay;
            }
            
            while (attacks.IsNotEmpty())
            {
                var to = (Square)attacks.GetLsbIndex();
                var captured = _position.GetPiece(to);
                
                if (captured == Piece.None)
                    moves.AddQuiet(from, to);
                else
                    moves.AddCapture(from, to, captured);
                    
                attacks = attacks.RemoveLsb();
            }
            
            pieces = pieces.RemoveLsb();
        }
    }
    
    private void GenerateKingMoves(ref MoveList moves)
    {
        var us = _position.SideToMove;
        var king = _position.GetBitboard(us, PieceType.King);
        
        if (king.IsEmpty()) return;
        
        var from = (Square)king.GetLsbIndex();
        var attacks = AttackTables.KingAttacks(from) & ~_position.GetBitboard(us);
        var occupancy = _position.OccupiedSquares();
        
        while (attacks.IsNotEmpty())
        {
            var to = (Square)attacks.GetLsbIndex();
            
            var captured = _position.GetPiece(to);
            if (captured == Piece.None)
                moves.AddQuiet(from, to);
            else
                moves.AddCapture(from, to, captured);
            
            attacks = attacks.RemoveLsb();
        }
        
        if (!_inCheck)
        {
            GenerateCastlingMoves(ref moves);
        }
    }
    
    private void GenerateCastlingMoves(ref MoveList moves)
    {
        var us = _position.SideToMove;
        var them = us == Color.White ? Color.Black : Color.White;
        var occupancy = _position.OccupiedSquares();
        
        if (us == Color.White)
        {
            if ((_position.CastlingRights & CastlingRights.WhiteKingside) != 0)
            {
                if ((occupancy & (Square.F1.ToBitboard() | Square.G1.ToBitboard())).IsEmpty() &&
                    !IsSquareAttacked(_position, Square.E1, them) &&
                    !IsSquareAttacked(_position, Square.F1, them) &&
                    !IsSquareAttacked(_position, Square.G1, them))
                {
                    moves.Add(Square.E1, Square.G1, MoveType.Castling);
                }
            }
            
            if ((_position.CastlingRights & CastlingRights.WhiteQueenside) != 0)
            {
                if ((occupancy & (Square.B1.ToBitboard() | Square.C1.ToBitboard() | Square.D1.ToBitboard())).IsEmpty() &&
                    !IsSquareAttacked(_position, Square.E1, them) &&
                    !IsSquareAttacked(_position, Square.D1, them) &&
                    !IsSquareAttacked(_position, Square.C1, them))
                {
                    moves.Add(Square.E1, Square.C1, MoveType.Castling);
                }
            }
        }
        else
        {
            if ((_position.CastlingRights & CastlingRights.BlackKingside) != 0)
            {
                if ((occupancy & (Square.F8.ToBitboard() | Square.G8.ToBitboard())).IsEmpty() &&
                    !IsSquareAttacked(_position, Square.E8, them) &&
                    !IsSquareAttacked(_position, Square.F8, them) &&
                    !IsSquareAttacked(_position, Square.G8, them))
                {
                    moves.Add(Square.E8, Square.G8, MoveType.Castling);
                }
            }
            
            if ((_position.CastlingRights & CastlingRights.BlackQueenside) != 0)
            {
                if ((occupancy & (Square.B8.ToBitboard() | Square.C8.ToBitboard() | Square.D8.ToBitboard())).IsEmpty() &&
                    !IsSquareAttacked(_position, Square.E8, them) &&
                    !IsSquareAttacked(_position, Square.D8, them) &&
                    !IsSquareAttacked(_position, Square.C8, them))
                {
                    moves.Add(Square.E8, Square.C8, MoveType.Castling);
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsMoveLegalWhenPinned(Square from, Square to)
    {
        var us = _position.SideToMove;
        var king = (Square)_position.GetBitboard(us, PieceType.King).GetLsbIndex();
        
        return (GetRayBetween(king, from) & to.ToBitboard()).IsNotEmpty() ||
               (GetRayExtension(king, from) & to.ToBitboard()).IsNotEmpty();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetRayExtension(Square from, Square to)
    {
        var fileDiff = to.File() - from.File();
        var rankDiff = to.Rank() - from.Rank();
        
        if (fileDiff == 0 && rankDiff == 0) return Bitboard.Empty;
        
        // Ensure the squares are on the same line (rank, file, or diagonal)
        if (fileDiff != 0 && rankDiff != 0 && 
            Math.Abs(fileDiff) != Math.Abs(rankDiff))
            return Bitboard.Empty;
        
        int direction;
        if (fileDiff == 0)
            direction = rankDiff > 0 ? AttackTables.Directions.North : AttackTables.Directions.South;
        else if (rankDiff == 0)
            direction = fileDiff > 0 ? AttackTables.Directions.East : AttackTables.Directions.West;
        else if (fileDiff == rankDiff)
            direction = fileDiff > 0 ? AttackTables.Directions.NorthEast : AttackTables.Directions.SouthWest;
        else // fileDiff == -rankDiff
            direction = fileDiff > 0 ? AttackTables.Directions.SouthEast : AttackTables.Directions.NorthWest;
            
        return AttackTables.GetRay(to, direction);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetFullRay(Square from, Square to)
    {
        var fileDiff = to.File() - from.File();
        var rankDiff = to.Rank() - from.Rank();
        
        if (fileDiff == 0 && rankDiff == 0) return Bitboard.Empty;
        
        // Ensure the squares are on the same line
        if (fileDiff != 0 && rankDiff != 0 && 
            Math.Abs(fileDiff) != Math.Abs(rankDiff))
            return Bitboard.Empty;
        
        int direction1, direction2;
        if (fileDiff == 0)
        {
            direction1 = rankDiff > 0 ? AttackTables.Directions.North : AttackTables.Directions.South;
            direction2 = rankDiff > 0 ? AttackTables.Directions.South : AttackTables.Directions.North;
        }
        else if (rankDiff == 0)
        {
            direction1 = fileDiff > 0 ? AttackTables.Directions.East : AttackTables.Directions.West;
            direction2 = fileDiff > 0 ? AttackTables.Directions.West : AttackTables.Directions.East;
        }
        else if (fileDiff == rankDiff)
        {
            direction1 = fileDiff > 0 ? AttackTables.Directions.NorthEast : AttackTables.Directions.SouthWest;
            direction2 = fileDiff > 0 ? AttackTables.Directions.SouthWest : AttackTables.Directions.NorthEast;
        }
        else // fileDiff == -rankDiff
        {
            direction1 = fileDiff > 0 ? AttackTables.Directions.SouthEast : AttackTables.Directions.NorthWest;
            direction2 = fileDiff > 0 ? AttackTables.Directions.NorthWest : AttackTables.Directions.SouthEast;
        }
        
        // Get rays in both directions from both squares
        return AttackTables.GetRay(from, direction1) | AttackTables.GetRay(from, direction2) |
               AttackTables.GetRay(to, direction1) | AttackTables.GetRay(to, direction2) |
               from.ToBitboard() | to.ToBitboard(); // Include the endpoints
    }
    
    private bool IsMoveLegal(Move move)
    {
        var us = _position.SideToMove;
        var them = us == Color.White ? Color.Black : Color.White;
        
        // Make the move temporarily
        var undoInfo = _position.MakeMove(move);
        
        // After making the move, we need to check if OUR king is attacked by the opponent
        var ourKing = _position.GetBitboard(us, PieceType.King);
        if (ourKing.IsEmpty())
        {
            // King was captured - this should never happen in legal chess
            _position.UnmakeMove(move, undoInfo);
            return false;
        }
        
        var kingSquare = (Square)ourKing.GetLsbIndex();
        var isLegal = !IsSquareAttacked(_position, kingSquare, them);
        
        // Restore the position
        _position.UnmakeMove(move, undoInfo);
        
        return isLegal;
    }
}