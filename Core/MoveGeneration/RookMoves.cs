namespace Meridian.Core.MoveGeneration;

/// <summary>
///    Generates rook moves using magic bitboards.
/// </summary>
public static class RookMoves
{
    /// <summary>
    ///    Generates all rook moves.
    /// </summary>
    public static void Generate(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var rooks = position.GetBitboard(PieceType.Rook, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Rook, sideToMove);
      var occupancy = position.Occupancy;

      while (rooks != 0)
      {
         var from = Bitboard.PopLsb(ref rooks);
         var attacks = MagicBitboards.GetRookAttacks(from, occupancy) & ~ourPieces;

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
    ///    Generates only rook captures.
    /// </summary>
    public static void GenerateCaptures(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var rooks = position.GetBitboard(PieceType.Rook, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Rook, sideToMove);
      var occupancy = position.Occupancy;

      while (rooks != 0)
      {
         var from = Bitboard.PopLsb(ref rooks);
         var attacks = MagicBitboards.GetRookAttacks(from, occupancy) & theirPieces;

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
