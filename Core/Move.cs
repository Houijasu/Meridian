namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
///    Move flags for special moves.
/// </summary>
[Flags]
public enum MoveFlags : byte
{
   None = 0,
   Capture = 1,
   DoublePawnPush = 2,
   EnPassantCapture = 4,
   KingsideCastle = 8,
   QueensideCastle = 16,
   PromotionToQueen = 32,
   PromotionToRook = 64,
   PromotionToBishop = 128,
   PromotionToKnight = PromotionToQueen | PromotionToRook, // Combined flags
   Promotion = PromotionToQueen | PromotionToRook | PromotionToBishop | PromotionToKnight,
   Castle = KingsideCastle | QueensideCastle
}

/// <summary>
///    Represents a chess move in a compact 32-bit format.
///    Layout: [8 bits flags][6 bits captured][6 bits to][6 bits from][6 bits piece]
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Move : IEquatable<Move>
{
   private readonly uint _data;

   /// <summary>
   ///    Represents a null/invalid move.
   /// </summary>
   public static readonly Move Null = new(0);

   /// <summary>
   ///    Creates a new move.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public Move(Square from, Square to, Piece piece, Piece captured = Piece.None, MoveFlags flags = MoveFlags.None) => _data = (uint)piece |
      (uint)from << 6 |
      (uint)to << 12 |
      (uint)captured << 18 |
      (uint)flags << 24;

   private Move(uint data) => _data = data;

   /// <summary>
   ///    Gets the piece being moved.
   /// </summary>
   public Piece Piece => (Piece)(_data & 0x3F);

   /// <summary>
   ///    Gets the source square.
   /// </summary>
   public Square From => (Square)(_data >> 6 & 0x3F);

   /// <summary>
   ///    Gets the destination square.
   /// </summary>
   public Square To => (Square)(_data >> 12 & 0x3F);

   /// <summary>
   ///    Gets the captured piece (if any).
   /// </summary>
   public Piece CapturedPiece => (Piece)(_data >> 18 & 0x3F);

   /// <summary>
   ///    Gets the move flags.
   /// </summary>
   public MoveFlags Flags => (MoveFlags)(_data >> 24 & 0xFF);

   /// <summary>
   ///    Checks if this is a capture move.
   /// </summary>
   public bool IsCapture => (Flags & MoveFlags.Capture) != 0 || CapturedPiece != Piece.None;

   /// <summary>
   ///    Checks if this is a promotion move.
   /// </summary>
   public bool IsPromotion => (Flags & MoveFlags.Promotion) != 0;

   /// <summary>
   ///    Checks if this is a castle move.
   /// </summary>
   public bool IsCastle => (Flags & MoveFlags.Castle) != 0;

   /// <summary>
   ///    Checks if this is an en passant capture.
   /// </summary>
   public bool IsEnPassant => (Flags & MoveFlags.EnPassantCapture) != 0;

   /// <summary>
   ///    Checks if this is a double pawn push.
   /// </summary>
   public bool IsDoublePawnPush => (Flags & MoveFlags.DoublePawnPush) != 0;

   /// <summary>
   ///    Checks if this is a null move.
   /// </summary>
   public bool IsNull => _data == 0;

   /// <summary>
   ///    Gets the promotion piece type (if this is a promotion).
   /// </summary>
   public PieceType GetPromotionType() => Flags switch {
      var f when (f & MoveFlags.PromotionToKnight) == MoveFlags.PromotionToKnight => PieceType.Knight,
      var f when (f & MoveFlags.PromotionToBishop) != 0 => PieceType.Bishop,
      var f when (f & MoveFlags.PromotionToRook) != 0 => PieceType.Rook,
      var f when (f & MoveFlags.PromotionToQueen) != 0 => PieceType.Queen,
      _ => PieceType.None
   };

   /// <summary>
   ///    Creates a quiet move (non-capture).
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Move CreateQuiet(Square from, Square to, Piece piece) =>
      new(from, to, piece);

   /// <summary>
   ///    Creates a capture move.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Move CreateCapture(Square from, Square to, Piece piece, Piece captured) =>
      new(from, to, piece, captured, MoveFlags.Capture);

   /// <summary>
   ///    Creates a double pawn push move.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Move CreateDoublePawnPush(Square from, Square to, Piece piece) =>
      new(from, to, piece, Piece.None, MoveFlags.DoublePawnPush);

   /// <summary>
   ///    Creates an en passant capture move.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Move CreateEnPassant(Square from, Square to, Piece piece, Piece captured) =>
      new(from, to, piece, captured, MoveFlags.EnPassantCapture | MoveFlags.Capture);

   /// <summary>
   ///    Creates a castle move.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Move CreateCastle(Square from, Square to, Piece piece, bool kingside) =>
      new(from, to, piece, Piece.None, kingside
         ? MoveFlags.KingsideCastle
         : MoveFlags.QueensideCastle);

   /// <summary>
   ///    Creates a promotion move.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Move CreatePromotion(Square from, Square to, Piece piece, PieceType promotionType, Piece captured = Piece.None)
   {
      var flags = promotionType switch {
         PieceType.Queen => MoveFlags.PromotionToQueen,
         PieceType.Rook => MoveFlags.PromotionToRook,
         PieceType.Bishop => MoveFlags.PromotionToBishop,
         PieceType.Knight => MoveFlags.PromotionToKnight,
         _ => MoveFlags.None
      };

      if (captured != Piece.None)
         flags |= MoveFlags.Capture;

      return new Move(from, to, piece, captured, flags);
   }

   /// <summary>
   ///    Converts the move to algebraic notation (e.g., "e2e4").
   /// </summary>
   public string ToAlgebraic()
   {
      if (IsNull) return "0000";

      var notation = $"{From.ToAlgebraic()}{To.ToAlgebraic()}";

      if (IsPromotion)
         notation += GetPromotionType() switch {
            PieceType.Queen => "q",
            PieceType.Rook => "r",
            PieceType.Bishop => "b",
            PieceType.Knight => "n",
            _ => ""
         };

      return notation;
   }

   /// <summary>
   ///    Parses a move from algebraic notation.
   /// </summary>
   public static Move ParseAlgebraic(ReadOnlySpan<char> notation, Position position)
   {
      if (notation.Length < 4)
         return Null;

      var from = SquareExtensions.ParseSquare(notation[..2]);
      var to = SquareExtensions.ParseSquare(notation[2..4]);
      var piece = position.GetPiece(from);
      var captured = position.GetPiece(to);

      // Handle promotions
      if (notation.Length > 4 && piece.Type() == PieceType.Pawn)
      {
         var promotionType = notation[4] switch {
            'q' or 'Q' => PieceType.Queen,
            'r' or 'R' => PieceType.Rook,
            'b' or 'B' => PieceType.Bishop,
            'n' or 'N' => PieceType.Knight,
            _ => PieceType.None
         };

         if (promotionType != PieceType.None)
            return CreatePromotion(from, to, piece, promotionType, captured);
      }

      // Handle special moves
      if (piece.Type() == PieceType.King && Math.Abs(from.File() - to.File()) == 2)
         return CreateCastle(from, to, piece, to.File() > from.File());

      if (piece.Type() == PieceType.Pawn)
      {
         if (Math.Abs(from.Rank() - to.Rank()) == 2)
            return CreateDoublePawnPush(from, to, piece);

         if (to == position.EnPassantSquare && captured == Piece.None)
            return CreateEnPassant(from, to, piece, piece.Color() == Color.White
               ? Piece.BlackPawn
               : Piece.WhitePawn);
      }

      return captured != Piece.None
         ? CreateCapture(from, to, piece, captured)
         : CreateQuiet(from, to, piece);
   }

   public bool Equals(Move other) => _data == other._data;
   public override bool Equals(object? obj) => obj is Move move && Equals(move);
   public override int GetHashCode() => (int)_data;
   public static bool operator ==(Move left, Move right) => left.Equals(right);
   public static bool operator !=(Move left, Move right) => !left.Equals(right);

   public override string ToString() => ToAlgebraic();
}
