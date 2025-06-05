namespace Meridian.Core.MoveGeneration;

/// <summary>
///    Generates bishop moves using magic bitboards.
/// </summary>
public static class BishopMoves
{
    /// <summary>
    ///    Generates all bishop moves.
    /// </summary>
    public static void Generate(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var bishops = position.GetBitboard(PieceType.Bishop, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Bishop, sideToMove);
      var occupancy = position.Occupancy;

      while (bishops != 0)
      {
         var from = Bitboard.PopLsb(ref bishops);
         var attacks = MagicBitboards.GetBishopAttacks(from, occupancy) & ~ourPieces;

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
    ///    Generates only bishop captures.
    /// </summary>
    public static void GenerateCaptures(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var bishops = position.GetBitboard(PieceType.Bishop, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Bishop, sideToMove);
      var occupancy = position.Occupancy;

      while (bishops != 0)
      {
         var from = Bitboard.PopLsb(ref bishops);
         var attacks = MagicBitboards.GetBishopAttacks(from, occupancy) & theirPieces;

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
