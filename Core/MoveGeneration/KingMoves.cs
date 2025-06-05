namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Generates king moves including castling.
/// </summary>
public static class KingMoves
{
    /// <summary>
    ///    Pre-calculated king attacks for each square.
    /// </summary>
    private static readonly ulong[] AttackTable = InitializeAttackTable();

    /// <summary>
    ///    Gets king attacks from a square.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetAttacks(int square) => AttackTable[square];

    /// <summary>
    ///    Generates all king moves including castling.
    /// </summary>
    public static void Generate(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var king = position.GetBitboard(PieceType.King, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.King, sideToMove);

      if (king == 0) return; // No king (shouldn't happen in valid position)

      var from = Bitboard.GetLsb(king);
      var attacks = GetAttacks(from) & ~ourPieces; // Can't capture our own pieces

      // Generate regular moves
      while (attacks != 0)
      {
         var to = Bitboard.PopLsb(ref attacks);
         var targetSquare = (Square)to;
         var captured = position.GetPiece(targetSquare);

         if (captured != Piece.None)
            moveList.AddCapture((Square)from, targetSquare, piece, captured);
         else
            moveList.AddQuiet((Square)from, targetSquare, piece);
      }

      // Generate castling moves
      GenerateCastling(in position, ref moveList, sideToMove, (Square)from, piece);
   }

    /// <summary>
    ///    Generates only king captures.
    /// </summary>
    public static void GenerateCaptures(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var king = position.GetBitboard(PieceType.King, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.King, sideToMove);

      if (king == 0) return;

      var from = Bitboard.GetLsb(king);
      var attacks = GetAttacks(from) & theirPieces; // Only enemy pieces

      while (attacks != 0)
      {
         var to = Bitboard.PopLsb(ref attacks);
         var targetSquare = (Square)to;
         var captured = position.GetPiece(targetSquare);
         moveList.AddCapture((Square)from, targetSquare, piece, captured);
      }
   }

    /// <summary>
    ///    Generates castling moves if legal.
    /// </summary>
    private static void GenerateCastling(in Position position, ref MoveList moveList,
      Color sideToMove, Square kingSquare, Piece king)
   {
      // Can't castle if in check
      if (AttackDetection.IsKingInCheck(in position, sideToMove))
         return;

      var occupancy = position.Occupancy;
      var enemyColor = sideToMove.Flip();

      if (sideToMove == Color.White)
      {
         // White kingside castling
         if ((position.CastlingRights & CastlingRights.WhiteKingside) != 0 &&
             kingSquare == Square.E1 &&
             (occupancy & 0x60UL) == 0 && // f1 and g1 must be empty
             !AttackDetection.IsSquareAttacked(in position, Square.F1, enemyColor) && // f1 not attacked
             !AttackDetection.IsSquareAttacked(in position, Square.G1, enemyColor)) // g1 not attacked
            moveList.Add(Move.CreateCastle(Square.E1, Square.G1, king, true));

         // White queenside castling
         if ((position.CastlingRights & CastlingRights.WhiteQueenside) != 0 &&
             kingSquare == Square.E1 &&
             (occupancy & 0x0EUL) == 0 && // b1, c1, and d1 must be empty
             !AttackDetection.IsSquareAttacked(in position, Square.D1, enemyColor) && // d1 not attacked
             !AttackDetection.IsSquareAttacked(in position, Square.C1, enemyColor)) // c1 not attacked
            moveList.Add(Move.CreateCastle(Square.E1, Square.C1, king, false));
      } else
      {
         // Black kingside castling
         if ((position.CastlingRights & CastlingRights.BlackKingside) != 0 &&
             kingSquare == Square.E8 &&
             (occupancy & 0x6000000000000000UL) == 0 && // f8 and g8 must be empty
             !AttackDetection.IsSquareAttacked(in position, Square.F8, enemyColor) && // f8 not attacked
             !AttackDetection.IsSquareAttacked(in position, Square.G8, enemyColor)) // g8 not attacked
            moveList.Add(Move.CreateCastle(Square.E8, Square.G8, king, true));

         // Black queenside castling
         if ((position.CastlingRights & CastlingRights.BlackQueenside) != 0 &&
             kingSquare == Square.E8 &&
             (occupancy & 0x0E00000000000000UL) == 0 && // b8, c8, and d8 must be empty
             !AttackDetection.IsSquareAttacked(in position, Square.D8, enemyColor) && // d8 not attacked
             !AttackDetection.IsSquareAttacked(in position, Square.C8, enemyColor)) // c8 not attacked
            moveList.Add(Move.CreateCastle(Square.E8, Square.C8, king, false));
      }
   }

    /// <summary>
    ///    Initializes the king attack table.
    /// </summary>
    private static ulong[] InitializeAttackTable()
   {
      var table = new ulong[64];

      for (var square = 0; square < 64; square++)
      {
         ulong attacks = 0;
         var rank = square / 8;
         var file = square % 8;

         // All 8 possible king moves
         int[] rankOffsets = [-1, -1, -1, 0, 0, 1, 1, 1];
         int[] fileOffsets = [-1, 0, 1, -1, 1, -1, 0, 1];

         for (var i = 0; i < 8; i++)
         {
            var newRank = rank + rankOffsets[i];
            var newFile = file + fileOffsets[i];

            if (newRank >= 0 && newRank < 8 && newFile >= 0 && newFile < 8)
            {
               var targetSquare = newRank * 8 + newFile;
               attacks |= 1UL << targetSquare;
            }
         }

         table[square] = attacks;
      }

      return table;
   }
}
