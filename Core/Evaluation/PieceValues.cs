namespace Meridian.Core.Evaluation;

/// <summary>
///    Standard piece values in centipawns (1 pawn = 100).
/// </summary>
public static class PieceValues
{
   public const int Pawn = 100;
   public const int Knight = 320;
   public const int Bishop = 330;
   public const int Rook = 500;
   public const int Queen = 900;
   public const int King = 20000; // Arbitrarily high value

   /// <summary>
   ///    Gets the material value of a piece type.
   /// </summary>
   public static int GetValue(PieceType pieceType) => pieceType switch {
      PieceType.Pawn => Pawn,
      PieceType.Knight => Knight,
      PieceType.Bishop => Bishop,
      PieceType.Rook => Rook,
      PieceType.Queen => Queen,
      PieceType.King => King,
      _ => 0
   };
}
