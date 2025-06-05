namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Helper methods for move generation.
/// </summary>
internal static class MoveGeneratorHelpers
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
}
