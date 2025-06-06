namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Generates knight moves using pre-calculated attack tables.
/// </summary>
public static class KnightMoves
{
    /// <summary>
    ///    Pre-calculated knight attacks for each square.
    /// </summary>
    private static readonly ulong[] AttackTable = InitializeAttackTable();

    /// <summary>
    ///    Gets knight attacks from a square.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetAttacks(int square) => AttackTable[square];

    /// <summary>
    ///    Generates all knight moves.
    /// </summary>
    public static void Generate(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var knights = position.GetBitboard(PieceType.Knight, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Knight, sideToMove);

      while (knights != 0)
      {
         var from = Bitboard.PopLsb(ref knights);
         var attacks = GetAttacks(from) & ~ourPieces; // Can't capture our own pieces

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
    ///    Generates only knight captures.
    /// </summary>
    public static void GenerateCaptures(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var knights = position.GetBitboard(PieceType.Knight, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Knight, sideToMove);

      while (knights != 0)
      {
         var from = Bitboard.PopLsb(ref knights);
         var attacks = GetAttacks(from) & theirPieces; // Only enemy pieces

         while (attacks != 0)
         {
            var to = Bitboard.PopLsb(ref attacks);
            var targetSquare = (Square)to;
            var captured = position.GetPiece(targetSquare);
            moveList.AddCapture((Square)from, targetSquare, piece, captured);
         }
      }
   }

    /// <summary>
    ///    Initializes the knight attack table.
    /// </summary>
    private static ulong[] InitializeAttackTable()
   {
      var table = new ulong[64];

      for (var square = 0; square < 64; square++)
      {
         ulong attacks = 0;
         var rank = square / 8;
         var file = square % 8;

         // All 8 possible knight moves
         int[] rankOffsets = [-2, -2, -1, -1, 1, 1, 2, 2];
         int[] fileOffsets = [-1, 1, -2, 2, -2, 2, -1, 1];

         for (var i = 0; i < 8; i++)
         {
            var newRank = rank + rankOffsets[i];
            var newFile = file + fileOffsets[i];

            if (newRank is >= 0 and < 8 && newFile is >= 0 and < 8)
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
