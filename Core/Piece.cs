namespace Meridian.Core;

/// <summary>
///    Represents chess piece types.
/// </summary>
public enum PieceType
{
   None = 0,
   Pawn = 1,
   Knight = 2,
   Bishop = 3,
   Rook = 4,
   Queen = 5,
   King = 6
}

/// <summary>
///    Represents piece colors.
/// </summary>
public enum Color
{
   White = 0,
   Black = 1
}

/// <summary>
///    Represents a piece with both type and color.
/// </summary>
public enum Piece
{
   None = 0,
   WhitePawn = 1,
   WhiteKnight = 2,
   WhiteBishop = 3,
   WhiteRook = 4,
   WhiteQueen = 5,
   WhiteKing = 6,
   BlackPawn = 9,
   BlackKnight = 10,
   BlackBishop = 11,
   BlackRook = 12,
   BlackQueen = 13,
   BlackKing = 14
}

/// <summary>
///    Extension methods for piece operations.
/// </summary>
public static class PieceExtensions
{
    /// <summary>
    ///    Creates a piece from type and color.
    /// </summary>
    public static Piece CreatePiece(PieceType type, Color color) =>
      (Piece)((int)type | (int)color << 3);

    /// <summary>
    ///    Gets the piece type.
    /// </summary>
    public static PieceType Type(this Piece piece) => (PieceType)((int)piece & 7);

    /// <summary>
    ///    Gets the piece color.
    /// </summary>
    public static Color Color(this Piece piece) => (Color)((int)piece >> 3);

    /// <summary>
    ///    Flips the color of a piece.
    /// </summary>
    public static Piece FlipColor(this Piece piece) => piece switch {
      Piece.None => Piece.None,
      _ => (Piece)((int)piece ^ 8)
   };

    /// <summary>
    ///    Converts piece to FEN character.
    /// </summary>
    public static char ToFenChar(this Piece piece) => piece switch {
      Piece.WhitePawn => 'P',
      Piece.WhiteKnight => 'N',
      Piece.WhiteBishop => 'B',
      Piece.WhiteRook => 'R',
      Piece.WhiteQueen => 'Q',
      Piece.WhiteKing => 'K',
      Piece.BlackPawn => 'p',
      Piece.BlackKnight => 'n',
      Piece.BlackBishop => 'b',
      Piece.BlackRook => 'r',
      Piece.BlackQueen => 'q',
      Piece.BlackKing => 'k',
      _ => ' '
   };

    /// <summary>
    ///    Parses a piece from FEN character.
    /// </summary>
    public static Piece ParsePiece(char fenChar) => fenChar switch {
      'P' => Piece.WhitePawn,
      'N' => Piece.WhiteKnight,
      'B' => Piece.WhiteBishop,
      'R' => Piece.WhiteRook,
      'Q' => Piece.WhiteQueen,
      'K' => Piece.WhiteKing,
      'p' => Piece.BlackPawn,
      'n' => Piece.BlackKnight,
      'b' => Piece.BlackBishop,
      'r' => Piece.BlackRook,
      'q' => Piece.BlackQueen,
      'k' => Piece.BlackKing,
      _ => Piece.None
   };

    /// <summary>
    ///    Gets the Unicode symbol for a piece.
    /// </summary>
    public static string ToUnicodeSymbol(this Piece piece) => piece switch {
      Piece.WhitePawn => "♙",
      Piece.WhiteKnight => "♘",
      Piece.WhiteBishop => "♗",
      Piece.WhiteRook => "♖",
      Piece.WhiteQueen => "♕",
      Piece.WhiteKing => "♔",
      Piece.BlackPawn => "♟",
      Piece.BlackKnight => "♞",
      Piece.BlackBishop => "♝",
      Piece.BlackRook => "♜",
      Piece.BlackQueen => "♛",
      Piece.BlackKing => "♚",
      _ => " "
   };

    /// <summary>
    ///    Gets the material value of a piece in centipawns.
    /// </summary>
    public static int MaterialValue(this PieceType type) => type switch {
      PieceType.Pawn => 100,
      PieceType.Knight => 320,
      PieceType.Bishop => 330,
      PieceType.Rook => 500,
      PieceType.Queen => 900,
      PieceType.King => 20000,
      _ => 0
   };
}

/// <summary>
///    Extension methods for color operations.
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    ///    Flips the color.
    /// </summary>
    public static Color Flip(this Color color) => (Color)(1 - (int)color);

    /// <summary>
    ///    Converts color to string.
    /// </summary>
    public static string ToString(this Color color) => color switch {
      Color.White => "White",
      Color.Black => "Black",
      _ => "None"
   };
}
