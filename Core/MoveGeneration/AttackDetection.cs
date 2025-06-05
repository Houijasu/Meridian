namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Provides methods for detecting attacks and checks.
/// </summary>
public static class AttackDetection
{
    /// <summary>
    ///    Checks if a square is attacked by the given color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool IsSquareAttacked(in Position position, Square square, Color byColor)
   {
      if (square == Square.None) return false;

      var sq = (int)square;
      var occupancy = position.Occupancy;

      // Check pawn attacks
      var enemyPawns = position.GetBitboard(PieceType.Pawn, byColor);

      if ((PawnMoves.GetAttacks(sq, byColor.Flip()) & enemyPawns) != 0)
         return true;

      // Check knight attacks
      var enemyKnights = position.GetBitboard(PieceType.Knight, byColor);

      if ((KnightMoves.GetAttacks(sq) & enemyKnights) != 0)
         return true;

      // Check king attacks
      var enemyKing = position.GetBitboard(PieceType.King, byColor);

      if ((KingMoves.GetAttacks(sq) & enemyKing) != 0)
         return true;

      // Check bishop/queen diagonal attacks
      var bishopAttacks = MagicBitboards.GetBishopAttacks(sq, occupancy);
      var enemyBishops = position.GetBitboard(PieceType.Bishop, byColor);
      var enemyQueens = position.GetBitboard(PieceType.Queen, byColor);

      if ((bishopAttacks & (enemyBishops | enemyQueens)) != 0)
         return true;

      // Check rook/queen straight attacks
      var rookAttacks = MagicBitboards.GetRookAttacks(sq, occupancy);
      var enemyRooks = position.GetBitboard(PieceType.Rook, byColor);

      if ((rookAttacks & (enemyRooks | enemyQueens)) != 0)
         return true;

      return false;
   }

    /// <summary>
    ///    Checks if the king of the given color is in check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool IsKingInCheck(in Position position, Color kingColor)
   {
      var king = position.GetBitboard(PieceType.King, kingColor);
      if (king == 0) return false; // No king (shouldn't happen)

      var kingSquare = (Square)Bitboard.GetLsb(king);
      return IsSquareAttacked(in position, kingSquare, kingColor.Flip());
   }

    /// <summary>
    ///    Gets all squares attacked by the given color.
    /// </summary>
    public static ulong GetAttackedSquares(in Position position, Color byColor)
   {
      ulong attacked = 0;
      var occupancy = position.Occupancy;

      // Pawn attacks
      var pawns = position.GetBitboard(PieceType.Pawn, byColor);
      var pawnsCopy = pawns;

      while (pawnsCopy != 0)
      {
         var sq = Bitboard.PopLsb(ref pawnsCopy);
         attacked |= PawnMoves.GetAttacks(sq, byColor);
      }

      // Knight attacks
      var knights = position.GetBitboard(PieceType.Knight, byColor);

      while (knights != 0)
      {
         var sq = Bitboard.PopLsb(ref knights);
         attacked |= KnightMoves.GetAttacks(sq);
      }

      // Bishop attacks
      var bishops = position.GetBitboard(PieceType.Bishop, byColor);

      while (bishops != 0)
      {
         var sq = Bitboard.PopLsb(ref bishops);
         attacked |= MagicBitboards.GetBishopAttacks(sq, occupancy);
      }

      // Rook attacks
      var rooks = position.GetBitboard(PieceType.Rook, byColor);

      while (rooks != 0)
      {
         var sq = Bitboard.PopLsb(ref rooks);
         attacked |= MagicBitboards.GetRookAttacks(sq, occupancy);
      }

      // Queen attacks
      var queens = position.GetBitboard(PieceType.Queen, byColor);

      while (queens != 0)
      {
         var sq = Bitboard.PopLsb(ref queens);
         attacked |= MagicBitboards.GetQueenAttacks(sq, occupancy);
      }

      // King attacks
      var king = position.GetBitboard(PieceType.King, byColor);

      if (king != 0)
      {
         var sq = Bitboard.GetLsb(king);
         attacked |= KingMoves.GetAttacks(sq);
      }

      return attacked;
   }

    /// <summary>
    ///    Checks if a move is legal (doesn't leave king in check).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool IsMoveLegal(Position position, Move move)
   {
      // Make the move
      position.MakeMove(move);

      // Check if our king is in check after the move
      // Note: SideToMove has already been flipped by MakeMove
      var inCheck = IsKingInCheck(in position, position.SideToMove.Flip());

      return !inCheck;
   }
}
