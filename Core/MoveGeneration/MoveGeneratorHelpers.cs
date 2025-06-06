namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Helper methods for move generation.
/// </summary>
public static class MoveGeneratorHelpers
{
    /// <summary>
    ///    Generates only capture moves from a given attack bitboard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static void GenerateCapturesFromAttacks(ref MoveList moveList, ulong attacks,
      ulong theirPieces, Square from, Piece piece, in Position position)
   {
      var captures = attacks & theirPieces;

      while (captures != 0)
      {
         var to = (Square)Bitboard.PopLsb(ref captures);
         var captured = position.GetPiece(to);
         moveList.AddCapture(from, to, piece, captured);
      }
   }
   
   /// <summary>
   ///    Quickly validates if a move could be pseudo-legal without generating all moves.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static bool IsMovePseudoLegal(in Position position, Move move)
   {
      if (move == Move.Null || move.From == move.To)
         return false;
         
      var piece = position.GetPiece(move.From);
      if (piece == Piece.None)
         return false;
         
      // Check if the piece in the move matches what's on the board
      if (piece != move.Piece)
         return false;
         
      var pieceColor = piece.Color();
      if (pieceColor != position.SideToMove)
         return false;
         
      var targetPiece = position.GetPiece(move.To);
      if (targetPiece != Piece.None && targetPiece.Color() == pieceColor)
         return false;
         
      // For captures, verify the captured piece matches
      if (move.IsCapture && move.CapturedPiece != targetPiece && !move.IsEnPassant)
         return false;
         
      // Basic validation passed - the move is likely valid
      // We don't do full move generation here for performance reasons
      return true;
   }
}
