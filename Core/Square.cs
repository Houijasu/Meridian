namespace Meridian.Core;

/// <summary>
///    Represents a square on the chess board (0-63).
/// </summary>
public enum Square
{
   A1,
   B1,
   C1,
   D1,
   E1,
   F1,
   G1,
   H1,
   A2,
   B2,
   C2,
   D2,
   E2,
   F2,
   G2,
   H2,
   A3,
   B3,
   C3,
   D3,
   E3,
   F3,
   G3,
   H3,
   A4,
   B4,
   C4,
   D4,
   E4,
   F4,
   G4,
   H4,
   A5,
   B5,
   C5,
   D5,
   E5,
   F5,
   G5,
   H5,
   A6,
   B6,
   C6,
   D6,
   E6,
   F6,
   G6,
   H6,
   A7,
   B7,
   C7,
   D7,
   E7,
   F7,
   G7,
   H7,
   A8,
   B8,
   C8,
   D8,
   E8,
   F8,
   G8,
   H8,
   None = 64
}

/// <summary>
///    Provides utility methods for working with squares.
/// </summary>
public static class SquareExtensions
{
    /// <summary>
    ///    Creates a square from file and rank indices.
    /// </summary>
    public static Square CreateSquare(int file, int rank) => (Square)(rank * 8 + file);

    /// <summary>
    ///    Gets the file index (0-7) of a square.
    /// </summary>
    public static int File(this Square square) => (int)square & 7;

    /// <summary>
    ///    Gets the rank index (0-7) of a square.
    /// </summary>
    public static int Rank(this Square square) => (int)square >> 3;

    /// <summary>
    ///    Converts a square to algebraic notation (e.g., "e4").
    /// </summary>
    public static string ToAlgebraic(this Square square) => square switch {
      Square.None => "-",
      _ => $"{(char)('a' + square.File())}{square.Rank() + 1}"
   };

    /// <summary>
    ///    Parses a square from algebraic notation.
    /// </summary>
    public static Square ParseSquare(ReadOnlySpan<char> algebraic) => algebraic switch {
      { Length: 2 } when algebraic[0] >= 'a' && algebraic[0] <= 'h' &&
                         algebraic[1] >= '1' && algebraic[1] <= '8'
         => CreateSquare(algebraic[0] - 'a', algebraic[1] - '1'),
      "-" => Square.None,
      _ => throw new ArgumentException($"Invalid square notation: {algebraic}")
   };

    /// <summary>
    ///    Flips a square vertically (for perspective changes).
    /// </summary>
    public static Square FlipVertical(this Square square) => (Square)((int)square ^ 56);

    /// <summary>
    ///    Flips a square horizontally.
    /// </summary>
    public static Square FlipHorizontal(this Square square) => (Square)((int)square ^ 7);

    /// <summary>
    ///    Gets the color of a square on the board (for display purposes).
    /// </summary>
    public static bool IsLightSquare(this Square square)
   {
      var file = square.File();
      var rank = square.Rank();
      return (file + rank) % 2 == 0;
   }

    /// <summary>
    ///    Calculates the distance between two squares.
    /// </summary>
    public static int Distance(this Square from, Square to)
   {
      var fileDiff = Math.Abs(from.File() - to.File());
      var rankDiff = Math.Abs(from.Rank() - to.Rank());
      return Math.Max(fileDiff, rankDiff);
   }

    /// <summary>
    ///    Calculates the Manhattan distance between two squares.
    /// </summary>
    public static int ManhattanDistance(this Square from, Square to)
   {
      var fileDiff = Math.Abs(from.File() - to.File());
      var rankDiff = Math.Abs(from.Rank() - to.Rank());
      return fileDiff + rankDiff;
   }
}
