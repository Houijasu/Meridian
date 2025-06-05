namespace Meridian.Core.MoveGeneration;

/// <summary>
///    Generates queen moves using magic bitboards (combination of rook and bishop moves).
/// </summary>
public static class QueenMoves
{
    /// <summary>
    ///    Generates all queen moves.
    /// </summary>
    public static void Generate(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var queens = position.GetBitboard(PieceType.Queen, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Queen, sideToMove);
      var occupancy = position.Occupancy;

      while (queens != 0)
      {
         var from = Bitboard.PopLsb(ref queens);
         var attacks = MagicBitboards.GetQueenAttacks(from, occupancy) & ~ourPieces;

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
      }
   }

    /// <summary>
    ///    Generates only queen captures.
    /// </summary>
    public static void GenerateCaptures(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var queens = position.GetBitboard(PieceType.Queen, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Queen, sideToMove);
      var occupancy = position.Occupancy;

      while (queens != 0)
      {
         var from = Bitboard.PopLsb(ref queens);
         var attacks = MagicBitboards.GetQueenAttacks(from, occupancy) & theirPieces;

         while (attacks != 0)
         {
            var to = Bitboard.PopLsb(ref attacks);
            var targetSquare = (Square)to;
            var captured = position.GetPiece(targetSquare);
            moveList.AddCapture((Square)from, targetSquare, piece, captured);
         }
      }
   }
}
