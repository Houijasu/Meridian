namespace Meridian.Core;

using System.Runtime.CompilerServices;

/// <summary>
///    Zobrist hashing for position keys.
///    Uses pre-generated random numbers for each piece on each square.
/// </summary>
public static class Zobrist
{
   private static readonly ulong[,] PieceKeys = new ulong[12, 64];
   private static readonly ulong BlackToMoveKey;
   private static readonly ulong[] CastlingKeys = new ulong[16];
   private static readonly ulong[] EnPassantKeys = new ulong[8];

   static Zobrist()
   {
      var rng = new Random(0x12345678);

      for (var piece = 0; piece < 12; piece++)
      {
         for (var square = 0; square < 64; square++)
         {
            PieceKeys[piece, square] = NextRandom64(rng);
         }
      }

      BlackToMoveKey = NextRandom64(rng);

      for (var i = 0; i < 16; i++)
      {
         CastlingKeys[i] = NextRandom64(rng);
      }

      for (var i = 0; i < 8; i++)
      {
         EnPassantKeys[i] = NextRandom64(rng);
      }
   }

   private static ulong NextRandom64(Random rng)
   {
      Span<byte> bytes = stackalloc byte[8];
      rng.NextBytes(bytes);
      return BitConverter.ToUInt64(bytes);
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static int PieceToIndex(Piece piece) => piece switch {
      Piece.WhitePawn => 0,
      Piece.WhiteKnight => 1,
      Piece.WhiteBishop => 2,
      Piece.WhiteRook => 3,
      Piece.WhiteQueen => 4,
      Piece.WhiteKing => 5,
      Piece.BlackPawn => 6,
      Piece.BlackKnight => 7,
      Piece.BlackBishop => 8,
      Piece.BlackRook => 9,
      Piece.BlackQueen => 10,
      Piece.BlackKing => 11,
      _ => -1
   };

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetPieceKey(Piece piece, Square square)
   {
      var pieceIndex = PieceToIndex(piece);

      return pieceIndex >= 0
         ? PieceKeys[pieceIndex, (int)square]
         : 0;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetSideKey(Color sideToMove) => sideToMove == Color.Black
      ? BlackToMoveKey
      : 0;

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetCastlingKey(CastlingRights rights) => CastlingKeys[(int)rights];

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong GetEnPassantKey(Square square) => square != Square.None
      ? EnPassantKeys[square.File()]
      : 0;

   public static ulong ComputeHash(ref Position position)
   {
      ulong hash = 0;

      for (var sq = 0; sq < 64; sq++)
      {
         var square = (Square)sq;
         var piece = position.GetPiece(square);

         if (piece != Piece.None)
            hash ^= GetPieceKey(piece, square);
      }

      hash ^= GetSideKey(position.SideToMove);
      hash ^= GetCastlingKey(position.CastlingRights);
      hash ^= GetEnPassantKey(position.EnPassantSquare);

      return hash;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong UpdateHash(ulong hash, Piece piece, Square from, Square to)
   {
      hash ^= GetPieceKey(piece, from);
      hash ^= GetPieceKey(piece, to);
      return hash;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong UpdateHashCapture(ulong hash, Piece movingPiece, Square from, Square to, Piece capturedPiece)
   {
      hash ^= GetPieceKey(movingPiece, from);
      hash ^= GetPieceKey(movingPiece, to);
      hash ^= GetPieceKey(capturedPiece, to);
      return hash;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong UpdateHashPromotion(ulong hash, Piece pawn, Square from, Square to, Piece promotedPiece)
   {
      hash ^= GetPieceKey(pawn, from);
      hash ^= GetPieceKey(promotedPiece, to);
      return hash;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong UpdateHashCastle(ulong hash, Color color, bool kingSide)
   {
      if (color == Color.White)
      {
         if (kingSide)
         {
            hash ^= GetPieceKey(Piece.WhiteKing, Square.E1);
            hash ^= GetPieceKey(Piece.WhiteKing, Square.G1);
            hash ^= GetPieceKey(Piece.WhiteRook, Square.H1);
            hash ^= GetPieceKey(Piece.WhiteRook, Square.F1);
         } else
         {
            hash ^= GetPieceKey(Piece.WhiteKing, Square.E1);
            hash ^= GetPieceKey(Piece.WhiteKing, Square.C1);
            hash ^= GetPieceKey(Piece.WhiteRook, Square.A1);
            hash ^= GetPieceKey(Piece.WhiteRook, Square.D1);
         }
      } else
      {
         if (kingSide)
         {
            hash ^= GetPieceKey(Piece.BlackKing, Square.E8);
            hash ^= GetPieceKey(Piece.BlackKing, Square.G8);
            hash ^= GetPieceKey(Piece.BlackRook, Square.H8);
            hash ^= GetPieceKey(Piece.BlackRook, Square.F8);
         } else
         {
            hash ^= GetPieceKey(Piece.BlackKing, Square.E8);
            hash ^= GetPieceKey(Piece.BlackKing, Square.C8);
            hash ^= GetPieceKey(Piece.BlackRook, Square.A8);
            hash ^= GetPieceKey(Piece.BlackRook, Square.D8);
         }
      }

      return hash;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static ulong UpdateHashEnPassant(ulong hash, Color color, Square from, Square to)
   {
      var pawn = color == Color.White
         ? Piece.WhitePawn
         : Piece.BlackPawn;

      var capturedPawn = color == Color.White
         ? Piece.BlackPawn
         : Piece.WhitePawn;

      var captureSquare = color == Color.White
         ? (Square)((int)to - 8)
         : (Square)((int)to + 8);

      hash ^= GetPieceKey(pawn, from);
      hash ^= GetPieceKey(pawn, to);
      hash ^= GetPieceKey(capturedPawn, captureSquare);

      return hash;
   }
}
