namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Generates pawn moves including special moves (promotions, en passant, double push).
/// </summary>
public static class PawnMoves
{
    /// <summary>
    ///    Pre-calculated pawn attacks for each square and color.
    /// </summary>
    private static readonly ulong[,] AttackTable = InitializeAttackTable();

    /// <summary>
    ///    Gets pawn attacks from a square.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetAttacks(int square, Color color) => AttackTable[(int)color, square];

    /// <summary>
    ///    Generates all pawn moves.
    /// </summary>
    public static void Generate(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces, ulong occupancy)
   {
      var pawns = position.GetBitboard(PieceType.Pawn, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Pawn, sideToMove);
      var empty = ~occupancy;

      if (sideToMove == Color.White)
         GenerateWhitePawnMoves(in position, ref moveList, pawns, piece, empty, theirPieces);
      else
         GenerateBlackPawnMoves(in position, ref moveList, pawns, piece, empty, theirPieces);
   }

    /// <summary>
    ///    Generates only pawn captures.
    /// </summary>
    public static void GenerateCaptures(in Position position, ref MoveList moveList,
      Color sideToMove, ulong ourPieces, ulong theirPieces)
   {
      var pawns = position.GetBitboard(PieceType.Pawn, sideToMove);
      var piece = PieceExtensions.CreatePiece(PieceType.Pawn, sideToMove);

      if (sideToMove == Color.White)
         GenerateWhitePawnCaptures(in position, ref moveList, pawns, piece, theirPieces);
      else
         GenerateBlackPawnCaptures(in position, ref moveList, pawns, piece, theirPieces);
   }

   private static void GenerateWhitePawnMoves(in Position position, ref MoveList moveList,
      ulong pawns, Piece piece, ulong empty, ulong theirPieces)
   {
      // Single push
      var singlePush = Bitboard.ShiftNorth(pawns) & empty;
      var doublePush = Bitboard.ShiftNorth(singlePush & Ranks.Rank3) & empty;

      // Generate single pushes
      var pushes = singlePush & ~Ranks.Rank8; // Non-promotion pushes

      while (pushes != 0)
      {
         var to = Bitboard.PopLsb(ref pushes);
         var from = to - 8;
         moveList.AddQuiet((Square)from, (Square)to, piece);
      }

      // Generate promotion pushes
      var promotions = singlePush & Ranks.Rank8;

      while (promotions != 0)
      {
         var to = Bitboard.PopLsb(ref promotions);
         var from = to - 8;
         moveList.AddPromotions((Square)from, (Square)to, piece);
      }

      // Generate double pushes
      while (doublePush != 0)
      {
         var to = Bitboard.PopLsb(ref doublePush);
         var from = to - 16;
         moveList.Add(Move.CreateDoublePawnPush((Square)from, (Square)to, piece));
      }

      // Generate captures
      GenerateWhitePawnCaptures(in position, ref moveList, pawns, piece, theirPieces);
   }

   private static void GenerateBlackPawnMoves(in Position position, ref MoveList moveList,
      ulong pawns, Piece piece, ulong empty, ulong theirPieces)
   {
      // Single push
      var singlePush = Bitboard.ShiftSouth(pawns) & empty;
      var doublePush = Bitboard.ShiftSouth(singlePush & Ranks.Rank6) & empty;

      // Generate single pushes
      var pushes = singlePush & ~Ranks.Rank1; // Non-promotion pushes

      while (pushes != 0)
      {
         var to = Bitboard.PopLsb(ref pushes);
         var from = to + 8;
         moveList.AddQuiet((Square)from, (Square)to, piece);
      }

      // Generate promotion pushes
      var promotions = singlePush & Ranks.Rank1;

      while (promotions != 0)
      {
         var to = Bitboard.PopLsb(ref promotions);
         var from = to + 8;
         moveList.AddPromotions((Square)from, (Square)to, piece);
      }

      // Generate double pushes
      while (doublePush != 0)
      {
         var to = Bitboard.PopLsb(ref doublePush);
         var from = to + 16;
         moveList.Add(Move.CreateDoublePawnPush((Square)from, (Square)to, piece));
      }

      // Generate captures
      GenerateBlackPawnCaptures(in position, ref moveList, pawns, piece, theirPieces);
   }

   private static void GenerateWhitePawnCaptures(in Position position, ref MoveList moveList,
      ulong pawns, Piece piece, ulong theirPieces)
   {
      // Left captures
      var leftCaptures = Bitboard.ShiftNorthWest(pawns) & theirPieces;
      var leftPromotions = leftCaptures & Ranks.Rank8;
      leftCaptures &= ~Ranks.Rank8;

      while (leftCaptures != 0)
      {
         var to = Bitboard.PopLsb(ref leftCaptures);
         var from = to - 7;
         var captured = position.GetPiece((Square)to);
         moveList.AddCapture((Square)from, (Square)to, piece, captured);
      }

      while (leftPromotions != 0)
      {
         var to = Bitboard.PopLsb(ref leftPromotions);
         var from = to - 7;
         var captured = position.GetPiece((Square)to);
         moveList.AddPromotions((Square)from, (Square)to, piece, captured);
      }

      // Right captures
      var rightCaptures = Bitboard.ShiftNorthEast(pawns) & theirPieces;
      var rightPromotions = rightCaptures & Ranks.Rank8;
      rightCaptures &= ~Ranks.Rank8;

      while (rightCaptures != 0)
      {
         var to = Bitboard.PopLsb(ref rightCaptures);
         var from = to - 9;
         var captured = position.GetPiece((Square)to);
         moveList.AddCapture((Square)from, (Square)to, piece, captured);
      }

      while (rightPromotions != 0)
      {
         var to = Bitboard.PopLsb(ref rightPromotions);
         var from = to - 9;
         var captured = position.GetPiece((Square)to);
         moveList.AddPromotions((Square)from, (Square)to, piece, captured);
      }

      // En passant
      if (position.EnPassantSquare != Square.None && position.EnPassantSquare.Rank() == 5)
      {
         var epSquare = (int)position.EnPassantSquare;
         var epBit = 1UL << epSquare;

         // Check if we can capture en passant from the left
         if (epSquare % 8 > 0 && Bitboard.TestBit(pawns, epSquare - 9))
            moveList.Add(Move.CreateEnPassant((Square)(epSquare - 9), position.EnPassantSquare, piece, Piece.BlackPawn));

         // Check if we can capture en passant from the right
         if (epSquare % 8 < 7 && Bitboard.TestBit(pawns, epSquare - 7))
            moveList.Add(Move.CreateEnPassant((Square)(epSquare - 7), position.EnPassantSquare, piece, Piece.BlackPawn));
      }
   }

   private static void GenerateBlackPawnCaptures(in Position position, ref MoveList moveList,
      ulong pawns, Piece piece, ulong theirPieces)
   {
      // Left captures
      var leftCaptures = Bitboard.ShiftSouthEast(pawns) & theirPieces;
      var leftPromotions = leftCaptures & Ranks.Rank1;
      leftCaptures &= ~Ranks.Rank1;

      while (leftCaptures != 0)
      {
         var to = Bitboard.PopLsb(ref leftCaptures);
         var from = to + 7;
         var captured = position.GetPiece((Square)to);
         moveList.AddCapture((Square)from, (Square)to, piece, captured);
      }

      while (leftPromotions != 0)
      {
         var to = Bitboard.PopLsb(ref leftPromotions);
         var from = to + 7;
         var captured = position.GetPiece((Square)to);
         moveList.AddPromotions((Square)from, (Square)to, piece, captured);
      }

      // Right captures
      var rightCaptures = Bitboard.ShiftSouthWest(pawns) & theirPieces;
      var rightPromotions = rightCaptures & Ranks.Rank1;
      rightCaptures &= ~Ranks.Rank1;

      while (rightCaptures != 0)
      {
         var to = Bitboard.PopLsb(ref rightCaptures);
         var from = to + 9;
         var captured = position.GetPiece((Square)to);
         moveList.AddCapture((Square)from, (Square)to, piece, captured);
      }

      while (rightPromotions != 0)
      {
         var to = Bitboard.PopLsb(ref rightPromotions);
         var from = to + 9;
         var captured = position.GetPiece((Square)to);
         moveList.AddPromotions((Square)from, (Square)to, piece, captured);
      }

      // En passant
      if (position.EnPassantSquare != Square.None && position.EnPassantSquare.Rank() == 2)
      {
         var epSquare = (int)position.EnPassantSquare;
         var epBit = 1UL << epSquare;

         // Check if we can capture en passant from the left
         if (epSquare % 8 < 7 && Bitboard.TestBit(pawns, epSquare + 9))
            moveList.Add(Move.CreateEnPassant((Square)(epSquare + 9), position.EnPassantSquare, piece, Piece.WhitePawn));

         // Check if we can capture en passant from the right
         if (epSquare % 8 > 0 && Bitboard.TestBit(pawns, epSquare + 7))
            moveList.Add(Move.CreateEnPassant((Square)(epSquare + 7), position.EnPassantSquare, piece, Piece.WhitePawn));
      }
   }

   /// <summary>
   ///    Initializes the pawn attack table.
   /// </summary>
   private static ulong[,] InitializeAttackTable()
   {
      var table = new ulong[2, 64];

      for (var square = 0; square < 64; square++)
      {
         var rank = square / 8;
         var file = square % 8;

         // White pawn attacks
         ulong whiteAttacks = 0;

         if (rank < 7) // Can attack forward
         {
            if (file > 0) whiteAttacks |= 1UL << square + 7; // Left attack
            if (file < 7) whiteAttacks |= 1UL << square + 9; // Right attack
         }

         table[0, square] = whiteAttacks;

         // Black pawn attacks
         ulong blackAttacks = 0;

         if (rank > 0) // Can attack backward
         {
            if (file < 7) blackAttacks |= 1UL << square - 7; // Left attack
            if (file > 0) blackAttacks |= 1UL << square - 9; // Right attack
         }

         table[1, square] = blackAttacks;
      }

      return table;
   }
}
