namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
///    Represents castling rights using bit flags.
/// </summary>
[Flags]
public enum CastlingRights : byte
{
   None = 0,
   WhiteKingside = 1,
   WhiteQueenside = 2,
   BlackKingside = 4,
   BlackQueenside = 8,
   White = WhiteKingside | WhiteQueenside,
   Black = BlackKingside | BlackQueenside,
   All = White | Black
}

/// <summary>
///    Represents a chess position using bitboards.
///    This struct is designed for high performance with careful memory layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct Position
{
   public ulong WhitePawns;
   public ulong WhiteKnights;
   public ulong WhiteBishops;
   public ulong WhiteRooks;
   public ulong WhiteQueens;
   public ulong WhiteKing;
   public ulong BlackPawns;
   public ulong BlackKnights;
   public ulong BlackBishops;
   public ulong BlackRooks;
   public ulong BlackQueens;
   public ulong BlackKing;

   public Color SideToMove;
   public CastlingRights CastlingRights;
   public Square EnPassantSquare;
   public byte HalfmoveClock;
   public ushort FullmoveNumber;

   public ulong Hash;

   /// <summary>
   ///    Gets all white pieces as a single bitboard.
   /// </summary>
   public readonly ulong WhitePieces => WhitePawns | WhiteKnights | WhiteBishops | WhiteRooks | WhiteQueens | WhiteKing;

   /// <summary>
   ///    Gets all black pieces as a single bitboard.
   /// </summary>
   public readonly ulong BlackPieces => BlackPawns | BlackKnights | BlackBishops | BlackRooks | BlackQueens | BlackKing;

   /// <summary>
   ///    Gets all pieces as a single bitboard.
   /// </summary>
   public readonly ulong Occupancy => WhitePieces | BlackPieces;

   /// <summary>
   ///    Gets empty squares.
   /// </summary>
   public readonly ulong EmptySquares => ~Occupancy;

   /// <summary>
   ///    Gets the bitboard for a specific piece type and color.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public readonly ulong GetBitboard(PieceType type, Color color) => (type, color) switch {
      (PieceType.Pawn, Color.White) => WhitePawns,
      (PieceType.Knight, Color.White) => WhiteKnights,
      (PieceType.Bishop, Color.White) => WhiteBishops,
      (PieceType.Rook, Color.White) => WhiteRooks,
      (PieceType.Queen, Color.White) => WhiteQueens,
      (PieceType.King, Color.White) => WhiteKing,
      (PieceType.Pawn, Color.Black) => BlackPawns,
      (PieceType.Knight, Color.Black) => BlackKnights,
      (PieceType.Bishop, Color.Black) => BlackBishops,
      (PieceType.Rook, Color.Black) => BlackRooks,
      (PieceType.Queen, Color.Black) => BlackQueens,
      (PieceType.King, Color.Black) => BlackKing,
      _ => 0UL
   };

   /// <summary>
   ///    Gets the bitboard for a specific piece.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public readonly ulong GetBitboard(Piece piece) => piece switch {
      Piece.WhitePawn => WhitePawns,
      Piece.WhiteKnight => WhiteKnights,
      Piece.WhiteBishop => WhiteBishops,
      Piece.WhiteRook => WhiteRooks,
      Piece.WhiteQueen => WhiteQueens,
      Piece.WhiteKing => WhiteKing,
      Piece.BlackPawn => BlackPawns,
      Piece.BlackKnight => BlackKnights,
      Piece.BlackBishop => BlackBishops,
      Piece.BlackRook => BlackRooks,
      Piece.BlackQueen => BlackQueens,
      Piece.BlackKing => BlackKing,
      _ => 0UL
   };

   /// <summary>
   ///    Gets pieces of a specific color.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public readonly ulong GetColorBitboard(Color color) => color switch {
      Color.White => WhitePieces,
      Color.Black => BlackPieces,
      _ => 0UL
   };

   /// <summary>
   ///    Gets the king square for the given color.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public readonly Square GetKingSquare(Color color)
   {
      var king = color == Color.White
         ? WhiteKing
         : BlackKing;

      return king == 0
         ? Square.None
         : (Square)Bitboard.GetLsb(king);
   }

   /// <summary>
   ///    Gets the piece at a specific square.
   /// </summary>
   public readonly Piece GetPiece(Square square)
   {
      if (square == Square.None) return Piece.None;

      var bit = Bitboard.SquareBit((int)square);

      if ((WhitePieces & bit) != 0)
      {
         if ((WhitePawns & bit) != 0) return Piece.WhitePawn;
         if ((WhiteKnights & bit) != 0) return Piece.WhiteKnight;
         if ((WhiteBishops & bit) != 0) return Piece.WhiteBishop;
         if ((WhiteRooks & bit) != 0) return Piece.WhiteRook;
         if ((WhiteQueens & bit) != 0) return Piece.WhiteQueen;
         if ((WhiteKing & bit) != 0) return Piece.WhiteKing;
      }

      if ((BlackPieces & bit) != 0)
      {
         if ((BlackPawns & bit) != 0) return Piece.BlackPawn;
         if ((BlackKnights & bit) != 0) return Piece.BlackKnight;
         if ((BlackBishops & bit) != 0) return Piece.BlackBishop;
         if ((BlackRooks & bit) != 0) return Piece.BlackRook;
         if ((BlackQueens & bit) != 0) return Piece.BlackQueen;
         if ((BlackKing & bit) != 0) return Piece.BlackKing;
      }

      return Piece.None;
   }

   /// <summary>
   ///    Sets a piece at a specific square.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void SetPiece(Square square, Piece piece)
   {
      if (square == Square.None) return;

      var bit = Bitboard.SquareBit((int)square);
      ClearSquare(square);

      switch (piece)
      {
         case Piece.WhitePawn: WhitePawns |= bit; break;

         case Piece.WhiteKnight: WhiteKnights |= bit; break;

         case Piece.WhiteBishop: WhiteBishops |= bit; break;

         case Piece.WhiteRook: WhiteRooks |= bit; break;

         case Piece.WhiteQueen: WhiteQueens |= bit; break;

         case Piece.WhiteKing: WhiteKing |= bit; break;

         case Piece.BlackPawn: BlackPawns |= bit; break;

         case Piece.BlackKnight: BlackKnights |= bit; break;

         case Piece.BlackBishop: BlackBishops |= bit; break;

         case Piece.BlackRook: BlackRooks |= bit; break;

         case Piece.BlackQueen: BlackQueens |= bit; break;

         case Piece.BlackKing: BlackKing |= bit; break;
      }
   }

   /// <summary>
   ///    Clears a square.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void ClearSquare(Square square)
   {
      if (square == Square.None) return;

      var clearMask = ~Bitboard.SquareBit((int)square);
      WhitePawns &= clearMask;
      WhiteKnights &= clearMask;
      WhiteBishops &= clearMask;
      WhiteRooks &= clearMask;
      WhiteQueens &= clearMask;
      WhiteKing &= clearMask;
      BlackPawns &= clearMask;
      BlackKnights &= clearMask;
      BlackBishops &= clearMask;
      BlackRooks &= clearMask;
      BlackQueens &= clearMask;
      BlackKing &= clearMask;
   }

   /// <summary>
   ///    Computes a hash key for just the pawn structure.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public readonly ulong GetPawnHash()
   {
      ulong hash = 0;
      
      // Hash white pawns
      var whitePawns = WhitePawns;
      while (whitePawns != 0)
      {
         var sq = Bitboard.PopLsb(ref whitePawns);
         hash ^= Zobrist.GetPieceKey(Piece.WhitePawn, (Square)sq);
      }
      
      // Hash black pawns
      var blackPawns = BlackPawns;
      while (blackPawns != 0)
      {
         var sq = Bitboard.PopLsb(ref blackPawns);
         hash ^= Zobrist.GetPieceKey(Piece.BlackPawn, (Square)sq);
      }
      
      return hash;
   }

   /// <summary>
   ///    Creates the starting position.
   /// </summary>
   public static Position StartingPosition()
   {
      var pos = new Position {
         WhitePawns = 0x000000000000FF00UL,
         WhiteKnights = 0x0000000000000042UL,
         WhiteBishops = 0x0000000000000024UL,
         WhiteRooks = 0x0000000000000081UL,
         WhiteQueens = 0x0000000000000008UL,
         WhiteKing = 0x0000000000000010UL,
         BlackPawns = 0x00FF000000000000UL,
         BlackKnights = 0x4200000000000000UL,
         BlackBishops = 0x2400000000000000UL,
         BlackRooks = 0x8100000000000000UL,
         BlackQueens = 0x0800000000000000UL,
         BlackKing = 0x1000000000000000UL,
         SideToMove = Color.White,
         CastlingRights = CastlingRights.All,
         EnPassantSquare = Square.None,
         HalfmoveClock = 0,
         FullmoveNumber = 1
      };

      pos.Hash = Zobrist.ComputeHash(ref pos);
      return pos;
   }

   /// <summary>
   ///    Creates an empty position.
   /// </summary>
   public static Position Empty() => new() {
      SideToMove = Color.White,
      CastlingRights = CastlingRights.None,
      EnPassantSquare = Square.None,
      HalfmoveClock = 0,
      FullmoveNumber = 1
   };

   /// <summary>
   ///    Makes a move on the position.
   ///    Note: This doesn't validate legality - that's done by the move generator.
   /// </summary>
   public void MakeMove(Move move)
   {
      var from = move.From;
      var to = move.To;
      var piece = move.Piece;
      var captured = move.CapturedPiece;

      if (EnPassantSquare != Square.None)
         Hash ^= Zobrist.GetEnPassantKey(EnPassantSquare);

      Hash ^= Zobrist.GetCastlingKey(CastlingRights);

      ClearSquare(from);

      if (captured != Piece.None)
         ClearSquare(to);

      if (move.IsCastle)
      {
         Hash = Zobrist.UpdateHashCastle(Hash, SideToMove, move.Flags == MoveFlags.KingsideCastle);

         if (move.Flags == MoveFlags.KingsideCastle)
         {
            if (SideToMove == Color.White)
            {
               ClearSquare(Square.H1);
               SetPiece(Square.F1, Piece.WhiteRook);
            } else
            {
               ClearSquare(Square.H8);
               SetPiece(Square.F8, Piece.BlackRook);
            }
         } else
         {
            if (SideToMove == Color.White)
            {
               ClearSquare(Square.A1);
               SetPiece(Square.D1, Piece.WhiteRook);
            } else
            {
               ClearSquare(Square.A8);
               SetPiece(Square.D8, Piece.BlackRook);
            }
         }
      } else if (move.IsEnPassant)
      {
         Hash = Zobrist.UpdateHashEnPassant(Hash, SideToMove, from, to);

         var capturedSquare = SideToMove == Color.White
            ? (Square)((int)to - 8)
            : (Square)((int)to + 8);

         ClearSquare(capturedSquare);
      } else if (move.IsPromotion)
      {
         var promotedPiece = PieceExtensions.CreatePiece(move.GetPromotionType(), SideToMove);
         Hash = Zobrist.UpdateHashPromotion(Hash, piece, from, to, promotedPiece);
         SetPiece(to, promotedPiece);
      } else if (captured != Piece.None)
      {
         Hash = Zobrist.UpdateHashCapture(Hash, piece, from, to, captured);
         SetPiece(to, piece);
      } else
      {
         Hash = Zobrist.UpdateHash(Hash, piece, from, to);
         SetPiece(to, piece);
      }

      UpdateCastlingRights(move);
      UpdateEnPassant(move);

      Hash ^= Zobrist.GetCastlingKey(CastlingRights);

      if (EnPassantSquare != Square.None)
         Hash ^= Zobrist.GetEnPassantKey(EnPassantSquare);

      if (captured != Piece.None || piece.Type() == PieceType.Pawn)
         HalfmoveClock = 0;
      else
         HalfmoveClock++;

      if (SideToMove == Color.Black)
         FullmoveNumber++;

      Hash ^= Zobrist.GetSideKey(Color.Black);

      SideToMove = SideToMove.Flip();
   }

   /// <summary>
   ///    Updates castling rights after a move.
   /// </summary>
   private void UpdateCastlingRights(Move move)
   {
      if (move.Piece.Type() == PieceType.King)
      {
         if (move.Piece.Color() == Color.White)
            CastlingRights &= ~CastlingRights.White;
         else
            CastlingRights &= ~CastlingRights.Black;
      }

      switch (move.From)
      {
         case Square.A1: CastlingRights &= ~CastlingRights.WhiteQueenside; break;

         case Square.H1: CastlingRights &= ~CastlingRights.WhiteKingside; break;

         case Square.A8: CastlingRights &= ~CastlingRights.BlackQueenside; break;

         case Square.H8: CastlingRights &= ~CastlingRights.BlackKingside; break;
      }

      switch (move.To)
      {
         case Square.A1: CastlingRights &= ~CastlingRights.WhiteQueenside; break;

         case Square.H1: CastlingRights &= ~CastlingRights.WhiteKingside; break;

         case Square.A8: CastlingRights &= ~CastlingRights.BlackQueenside; break;

         case Square.H8: CastlingRights &= ~CastlingRights.BlackKingside; break;
      }
   }

   /// <summary>
   ///    Updates en passant square after a move.
   /// </summary>
   private void UpdateEnPassant(Move move)
   {
      if (move.IsDoublePawnPush)
         EnPassantSquare = SideToMove == Color.White
            ? (Square)((int)move.To - 8)
            : (Square)((int)move.To + 8);
      else
         EnPassantSquare = Square.None;
   }

   /// <summary>
   ///    Converts the position to a string representation (ASCII board).
   /// </summary>
   public override readonly string ToString()
   {
      var sb = new StringBuilder();
      sb.AppendLine("  a b c d e f g h");
      sb.AppendLine("  ---------------");

      for (var rank = 7; rank >= 0; rank--)
      {
         sb.Append($"{rank + 1}|");

         for (var file = 0; file < 8; file++)
         {
            var square = SquareExtensions.CreateSquare(file, rank);
            var piece = GetPiece(square);

            sb.Append(piece == Piece.None
               ? ". "
               : $"{piece.ToFenChar()} ");
         }

         sb.AppendLine($"|{rank + 1}");
      }

      sb.AppendLine("  ---------------");
      sb.AppendLine("  a b c d e f g h");
      sb.AppendLine($"Side to move: {SideToMove}");
      sb.AppendLine($"Castling: {CastlingRights}");
      sb.AppendLine($"En passant: {EnPassantSquare.ToAlgebraic()}");
      sb.AppendLine($"Halfmove clock: {HalfmoveClock}");
      sb.AppendLine($"Fullmove: {FullmoveNumber}");

      return sb.ToString();
   }
}
