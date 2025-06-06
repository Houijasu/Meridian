namespace Meridian.Core.Search;

using System.Numerics;
using System.Runtime.CompilerServices;
using Evaluation;
using MoveGeneration;

/// <summary>
/// Static Exchange Evaluation (SEE) for evaluating capture sequences.
/// </summary>
public static class StaticExchangeEvaluation
{
    /// <summary>
    /// Evaluates a capture move to determine if it's likely to win material.
    /// Returns the expected material gain/loss from the capture sequence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(in Position position, Move move)
    {
        // Special case: en passant captures always capture a pawn
        if (move.IsEnPassant)
            return PieceValues.Pawn;

        var to = move.To;
        var capturedValue = move.CapturedPiece.Type().MaterialValue();
        
        // If we're not capturing anything, SEE is 0
        if (capturedValue == 0)
            return 0;

        var from = move.From;
        var movingPiece = move.Piece;
        var movingValue = movingPiece.Type().MaterialValue();
        var sideToMove = position.SideToMove;

        // Make the initial capture virtually
        var occupancy = position.Occupancy;
        occupancy &= ~(1UL << (int)from);
        occupancy |= 1UL << (int)to;

        // Initialize the gain array
        // gains[0] = value of piece captured on first move
        // gains[1] = value of piece captured on first recapture - value of piece that made the first capture
        // etc.
        var gains = new int[32];
        gains[0] = capturedValue;

        // Find all attackers to the square
        var attackers = GetAttackers(in position, to, occupancy) & occupancy;
        
        // Remove the piece that just moved from attackers
        attackers &= ~(1UL << (int)from);

        var depth = 1;
        var currentSide = sideToMove.Flip();
        var lastCapturedValue = movingValue;

        while (attackers != 0)
        {
            // Find the least valuable attacker for the current side
            var leastValuableAttacker = GetLeastValuableAttacker(in position, attackers, currentSide);
            
            if (leastValuableAttacker == Square.None)
                break;

            // Remove this attacker
            attackers &= ~(1UL << (int)leastValuableAttacker);
            occupancy &= ~(1UL << (int)leastValuableAttacker);

            // Update attackers with x-ray attacks
            attackers |= GetXrayAttackers(in position, to, occupancy, leastValuableAttacker);

            // Record the gain
            gains[depth] = lastCapturedValue - gains[depth - 1];
            
            // Get the value of the attacking piece
            var attackerPiece = position.GetPiece(leastValuableAttacker);
            lastCapturedValue = attackerPiece.Type().MaterialValue();

            depth++;
            currentSide = currentSide.Flip();
        }

        // Negamax-like propagation of the gains
        while (--depth > 0)
        {
            gains[depth - 1] = -Math.Max(-gains[depth - 1], gains[depth]);
        }

        return gains[0];
    }

    /// <summary>
    /// Gets all pieces attacking a square with the given occupancy.
    /// </summary>
    private static ulong GetAttackers(in Position position, Square square, ulong occupancy)
    {
        ulong attackers = 0;

        // Pawn attacks
        var whitePawnAttacks = PawnMoves.GetAttacks((int)square, Color.Black);
        attackers |= whitePawnAttacks & position.WhitePawns;

        var blackPawnAttacks = PawnMoves.GetAttacks((int)square, Color.White);
        attackers |= blackPawnAttacks & position.BlackPawns;

        // Knight attacks
        var knightAttacks = KnightMoves.GetAttacks((int)square);
        attackers |= knightAttacks & (position.WhiteKnights | position.BlackKnights);

        // Bishop/Queen diagonal attacks
        var bishopAttacks = MagicBitboards.GetBishopAttacks((int)square, occupancy);
        attackers |= bishopAttacks & (position.WhiteBishops | position.BlackBishops | 
                                     position.WhiteQueens | position.BlackQueens);

        // Rook/Queen straight attacks
        var rookAttacks = MagicBitboards.GetRookAttacks((int)square, occupancy);
        attackers |= rookAttacks & (position.WhiteRooks | position.BlackRooks | 
                                   position.WhiteQueens | position.BlackQueens);

        // King attacks
        var kingAttacks = KingMoves.GetAttacks((int)square);
        attackers |= kingAttacks & (position.WhiteKing | position.BlackKing);

        return attackers;
    }

    /// <summary>
    /// Gets the least valuable piece of the given color attacking the square.
    /// </summary>
    private static Square GetLeastValuableAttacker(in Position position, ulong attackers, Color color)
    {
        // Check in order: pawns, knights, bishops, rooks, queens, king
        ulong colorPieces;
        ulong pieceBitboard;

        // Pawns
        colorPieces = color == Color.White ? position.WhitePawns : position.BlackPawns;
        pieceBitboard = attackers & colorPieces;
        if (pieceBitboard != 0)
            return (Square)BitOperations.TrailingZeroCount(pieceBitboard);

        // Knights
        colorPieces = color == Color.White ? position.WhiteKnights : position.BlackKnights;
        pieceBitboard = attackers & colorPieces;
        if (pieceBitboard != 0)
            return (Square)BitOperations.TrailingZeroCount(pieceBitboard);

        // Bishops
        colorPieces = color == Color.White ? position.WhiteBishops : position.BlackBishops;
        pieceBitboard = attackers & colorPieces;
        if (pieceBitboard != 0)
            return (Square)BitOperations.TrailingZeroCount(pieceBitboard);

        // Rooks
        colorPieces = color == Color.White ? position.WhiteRooks : position.BlackRooks;
        pieceBitboard = attackers & colorPieces;
        if (pieceBitboard != 0)
            return (Square)BitOperations.TrailingZeroCount(pieceBitboard);

        // Queens
        colorPieces = color == Color.White ? position.WhiteQueens : position.BlackQueens;
        pieceBitboard = attackers & colorPieces;
        if (pieceBitboard != 0)
            return (Square)BitOperations.TrailingZeroCount(pieceBitboard);

        // King
        colorPieces = color == Color.White ? position.WhiteKing : position.BlackKing;
        pieceBitboard = attackers & colorPieces;
        if (pieceBitboard != 0)
            return (Square)BitOperations.TrailingZeroCount(pieceBitboard);

        return Square.None;
    }

    /// <summary>
    /// Gets x-ray attackers revealed by removing a piece.
    /// </summary>
    private static ulong GetXrayAttackers(in Position position, Square target, ulong occupancy, Square removedSquare)
    {
        var removedPiece = position.GetPiece(removedSquare);
        var pieceType = removedPiece.Type();

        // Only sliders can create x-ray attacks
        if (pieceType != PieceType.Bishop && pieceType != PieceType.Rook && pieceType != PieceType.Queen)
            return 0;

        ulong xrayAttackers = 0;

        // Check if the removed piece was on the same diagonal as the target
        if (pieceType == PieceType.Bishop || pieceType == PieceType.Queen)
        {
            if (Math.Abs(removedSquare.Rank() - target.Rank()) == Math.Abs(removedSquare.File() - target.File()))
            {
                var bishopAttacks = MagicBitboards.GetBishopAttacks((int)target, occupancy);
                xrayAttackers |= bishopAttacks & (position.WhiteBishops | position.BlackBishops | 
                                                  position.WhiteQueens | position.BlackQueens);
            }
        }

        // Check if the removed piece was on the same rank/file as the target
        if (pieceType == PieceType.Rook || pieceType == PieceType.Queen)
        {
            if (removedSquare.Rank() == target.Rank() || removedSquare.File() == target.File())
            {
                var rookAttacks = MagicBitboards.GetRookAttacks((int)target, occupancy);
                xrayAttackers |= rookAttacks & (position.WhiteRooks | position.BlackRooks | 
                                               position.WhiteQueens | position.BlackQueens);
            }
        }

        return xrayAttackers & ~GetAttackers(in position, target, occupancy | (1UL << (int)removedSquare));
    }

    /// <summary>
    /// Quick check if a capture is obviously good (capturing a more valuable piece).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsObviouslyGood(Move move)
    {
        if (!move.IsCapture)
            return false;

        var capturedValue = move.CapturedPiece.Type().MaterialValue();
        var movingValue = move.Piece.Type().MaterialValue();

        // Capturing a more valuable or equal piece is always good
        return capturedValue >= movingValue;
    }

    /// <summary>
    /// Checks if SEE value is at least the threshold.
    /// More efficient than computing exact SEE when we only need to know if it's >= threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SeeGreaterOrEqual(in Position position, Move move, int threshold)
    {
        // Non-captures have SEE value of 0
        if (!move.IsCapture)
            return threshold <= 0;

        // Quick check for obviously good captures
        if (threshold <= 0 && IsObviouslyGood(move))
            return true;

        // For now, just compute exact SEE and compare
        // TODO: Optimize this with early cutoffs
        return Evaluate(in position, move) >= threshold;
    }
}