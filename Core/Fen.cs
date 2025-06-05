namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
///    Provides FEN (Forsyth-Edwards Notation) parsing and generation functionality.
/// </summary>
public static class Fen
{
    /// <summary>
    ///    The standard starting position FEN.
    /// </summary>
    public const string StartingPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>
    ///    Parses a FEN string into a Position.
    /// </summary>
    public static Position Parse(ReadOnlySpan<char> fen)
   {
      var position = Position.Empty();

      // Split FEN into parts
      var parts = new FenParts();
      SplitFen(fen, ref parts);

      // Parse piece placement
      ParsePiecePlacement(parts.PiecePlacement, ref position);

      // Parse side to move
      position.SideToMove = parts.SideToMove switch {
         "w" => Color.White,
         "b" => Color.Black,
         _ => throw new ArgumentException($"Invalid side to move: {parts.SideToMove}")
      };

      // Parse castling rights
      position.CastlingRights = ParseCastlingRights(parts.CastlingRights);

      // Parse en passant square
      position.EnPassantSquare = parts.EnPassant switch {
         "-" => Square.None,
         _ => SquareExtensions.ParseSquare(parts.EnPassant)
      };

      // Parse halfmove clock
      if (byte.TryParse(parts.HalfmoveClock, out var halfmove))
         position.HalfmoveClock = halfmove;
      else
         throw new ArgumentException($"Invalid halfmove clock: {parts.HalfmoveClock}");

      // Parse fullmove number
      if (ushort.TryParse(parts.FullmoveNumber, out var fullmove))
         position.FullmoveNumber = fullmove;
      else
         throw new ArgumentException($"Invalid fullmove number: {parts.FullmoveNumber}");

      // Compute hash
      position.Hash = Zobrist.ComputeHash(ref position);

      return position;
   }

    /// <summary>
    ///    Converts a Position to FEN string.
    /// </summary>
    public static string ToFen(ref readonly Position position)
   {
      var builder = new StringBuilder(100);

      // Piece placement
      for (var rank = 7; rank >= 0; rank--)
      {
         var emptyCount = 0;

         for (var file = 0; file < 8; file++)
         {
            var square = SquareExtensions.CreateSquare(file, rank);
            var piece = position.GetPiece(square);

            if (piece == Piece.None)
               emptyCount++;
            else
            {
               if (emptyCount > 0)
               {
                  builder.Append(emptyCount);
                  emptyCount = 0;
               }

               builder.Append(piece.ToFenChar());
            }
         }

         if (emptyCount > 0)
            builder.Append(emptyCount);

         if (rank > 0)
            builder.Append('/');
      }

      // Side to move
      builder.Append(' ');

      builder.Append(position.SideToMove == Color.White
         ? 'w'
         : 'b');

      // Castling rights
      builder.Append(' ');

      if (position.CastlingRights == CastlingRights.None)
         builder.Append('-');
      else
      {
         if ((position.CastlingRights & CastlingRights.WhiteKingside) != 0) builder.Append('K');
         if ((position.CastlingRights & CastlingRights.WhiteQueenside) != 0) builder.Append('Q');
         if ((position.CastlingRights & CastlingRights.BlackKingside) != 0) builder.Append('k');
         if ((position.CastlingRights & CastlingRights.BlackQueenside) != 0) builder.Append('q');
      }

      // En passant
      builder.Append(' ');

      builder.Append(position.EnPassantSquare == Square.None
         ? "-"
         : position.EnPassantSquare.ToAlgebraic());

      // Halfmove clock and fullmove number
      builder.Append(' ');
      builder.Append(position.HalfmoveClock);
      builder.Append(' ');
      builder.Append(position.FullmoveNumber);

      return builder.ToString();
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static void SplitFen(ReadOnlySpan<char> fen, ref FenParts parts)
   {
      var start = 0;
      var partIndex = 0;

      for (var i = 0; i <= fen.Length; i++)
      {
         if (i == fen.Length || fen[i] == ' ')
         {
            var part = fen[start..i];

            switch (partIndex)
            {
               case 0: parts.PiecePlacement = part; break;

               case 1: parts.SideToMove = part; break;

               case 2: parts.CastlingRights = part; break;

               case 3: parts.EnPassant = part; break;

               case 4: parts.HalfmoveClock = part; break;

               case 5: parts.FullmoveNumber = part; break;
            }

            start = i + 1;
            partIndex++;
         }
      }

      // EPD format might have only 4 parts (no halfmove/fullmove clocks)
      if (partIndex < 4)
         throw new ArgumentException($"Invalid FEN string: expected at least 4 parts, got {partIndex}");

      // Default values for missing parts
      if (partIndex == 4)
      {
         parts.HalfmoveClock = "0";
         parts.FullmoveNumber = "1";
      } else if (partIndex == 5)
         parts.FullmoveNumber = "1";
   }

   private static void ParsePiecePlacement(ReadOnlySpan<char> placement, ref Position position)
   {
      var rank = 7;
      var file = 0;

      foreach (var c in placement)
      {
         switch (c)
         {
            case '/':
               rank--;
               file = 0;
               break;

            case >= '1' and <= '8':
               file += c - '0';
               break;

            default:
               var piece = PieceExtensions.ParsePiece(c);

               if (piece != Piece.None)
               {
                  var square = SquareExtensions.CreateSquare(file, rank);
                  position.SetPiece(square, piece);
                  file++;
               } else
                  throw new ArgumentException($"Invalid piece character: {c}");

               break;
         }
      }
   }

   private static CastlingRights ParseCastlingRights(ReadOnlySpan<char> rights)
   {
      if (rights.Length == 1 && rights[0] == '-')
         return CastlingRights.None;

      var result = CastlingRights.None;

      foreach (var c in rights)
      {
         result |= c switch {
            'K' => CastlingRights.WhiteKingside,
            'Q' => CastlingRights.WhiteQueenside,
            'k' => CastlingRights.BlackKingside,
            'q' => CastlingRights.BlackQueenside,
            _ => throw new ArgumentException($"Invalid castling rights character: {c}")
         };
      }

      return result;
   }

   private ref struct FenParts
   {
      public ReadOnlySpan<char> PiecePlacement;
      public ReadOnlySpan<char> SideToMove;
      public ReadOnlySpan<char> CastlingRights;
      public ReadOnlySpan<char> EnPassant;
      public ReadOnlySpan<char> HalfmoveClock;
      public ReadOnlySpan<char> FullmoveNumber;
   }
}
