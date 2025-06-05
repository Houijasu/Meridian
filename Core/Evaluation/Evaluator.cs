namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;

/// <summary>
///    Static position evaluation.
/// </summary>
public static class Evaluator
{
    /// <summary>
    ///    Evaluates the position from the perspective of the side to move.
    ///    Returns positive values for advantages, negative for disadvantages.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static int Evaluate(in Position position)
   {
      var score = EvaluateAbsolute(in position);

      // Return score from perspective of side to move
      return position.SideToMove == Color.White
         ? score
         : -score;
   }

    /// <summary>
    ///    Evaluates the position from White's perspective.
    ///    Positive values favor White, negative values favor Black.
    /// </summary>
    public static int EvaluateAbsolute(in Position position)
   {
      var materialScore = 0;
      var positionalScore = 0;

      // Count material and apply piece-square tables
      materialScore += EvaluatePieces(position.WhitePawns, PieceType.Pawn, Color.White, ref positionalScore, position);
      materialScore += EvaluatePieces(position.WhiteKnights, PieceType.Knight, Color.White, ref positionalScore, position);
      materialScore += EvaluatePieces(position.WhiteBishops, PieceType.Bishop, Color.White, ref positionalScore, position);
      materialScore += EvaluatePieces(position.WhiteRooks, PieceType.Rook, Color.White, ref positionalScore, position);
      materialScore += EvaluatePieces(position.WhiteQueens, PieceType.Queen, Color.White, ref positionalScore, position);
      materialScore += EvaluatePieces(position.WhiteKing, PieceType.King, Color.White, ref positionalScore, position);

      materialScore -= EvaluatePieces(position.BlackPawns, PieceType.Pawn, Color.Black, ref positionalScore, position);
      materialScore -= EvaluatePieces(position.BlackKnights, PieceType.Knight, Color.Black, ref positionalScore, position);
      materialScore -= EvaluatePieces(position.BlackBishops, PieceType.Bishop, Color.Black, ref positionalScore, position);
      materialScore -= EvaluatePieces(position.BlackRooks, PieceType.Rook, Color.Black, ref positionalScore, position);
      materialScore -= EvaluatePieces(position.BlackQueens, PieceType.Queen, Color.Black, ref positionalScore, position);
      materialScore -= EvaluatePieces(position.BlackKing, PieceType.King, Color.Black, ref positionalScore, position);

      // Calculate endgame phase for tapered evaluation
      int endgamePhase = Endgame.GetEndgamePhase(in position);
      int middlegamePhase = 256 - endgamePhase;
      
      // Middlegame evaluation components
      int mgScore = materialScore + positionalScore;
      
      // Add pawn structure evaluation
      var pawnScore = PawnStructure.Evaluate(in position);
      mgScore += pawnScore;
      
      // Add king safety evaluation (primarily middlegame)
      var kingSafetyScore = KingSafety.Evaluate(in position);
      mgScore += kingSafetyScore;
      
      // Add mobility evaluation
      var mobilityScore = Mobility.Evaluate(in position);
      mgScore += mobilityScore;
      
      // Endgame evaluation components
      int egScore = materialScore + positionalScore + pawnScore;
      
      // Add endgame-specific evaluation
      var endgameScore = Endgame.Evaluate(in position);
      egScore += endgameScore;
      
      // Add reduced mobility in endgame
      egScore += mobilityScore / 2;
      
      // Tapered evaluation
      return (mgScore * middlegamePhase + egScore * endgamePhase) / 256;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static int EvaluatePieces(ulong bitboard, PieceType pieceType, Color color, ref int positionalScore, in Position position)
   {
      if (bitboard == 0) return 0;

      var count = Bitboard.PopCount(bitboard);
      var material = count * PieceValues.GetValue(pieceType);

      // Determine if we're in endgame (simplified: queens off or very few pieces)
      var isEndgame = position.WhiteQueens == 0 && position.BlackQueens == 0;

      // Add piece-square table values
      while (bitboard != 0)
      {
         var square = (Square)Bitboard.PopLsb(ref bitboard);
         positionalScore += PieceSquareTables.GetPieceSquareValue(pieceType, square, color, isEndgame);
      }

      return material;
   }

   /// <summary>
   ///    Checks if the position is a draw by insufficient material.
   /// </summary>
   public static bool IsInsufficientMaterial(in Position position)
   {
      // Quick check: if there are pawns, rooks, or queens, it's not insufficient material
      if ((position.WhitePawns | position.BlackPawns |
           position.WhiteRooks | position.BlackRooks |
           position.WhiteQueens | position.BlackQueens) != 0)
         return false;

      var whiteMinorPieces = Bitboard.PopCount(position.WhiteKnights | position.WhiteBishops);
      var blackMinorPieces = Bitboard.PopCount(position.BlackKnights | position.BlackBishops);

      // King vs King
      if (whiteMinorPieces == 0 && blackMinorPieces == 0)
         return true;

      // King and minor piece vs King
      if (whiteMinorPieces == 1 && blackMinorPieces == 0 ||
          whiteMinorPieces == 0 && blackMinorPieces == 1)
         return true;

      // King and two knights vs King (cannot force mate)
      if (whiteMinorPieces == 2 && blackMinorPieces == 0 && position.WhiteKnights == (position.WhitePieces ^ position.WhiteKing))
         return true;

      if (blackMinorPieces == 2 && whiteMinorPieces == 0 && position.BlackKnights == (position.BlackPieces ^ position.BlackKing))
         return true;

      // Both sides have only one bishop on same color squares
      if (whiteMinorPieces == 1 && blackMinorPieces == 1 &&
          position.WhiteBishops != 0 && position.BlackBishops != 0)
      {
         // Check if bishops are on same color
         var whiteBishopSquare = position.WhiteBishops;
         var blackBishopSquare = position.BlackBishops;

         // Light squares mask: 0x55AA55AA55AA55AA
         const ulong lightSquares = 0x55AA55AA55AA55AAUL;

         var whiteBishopOnLight = (whiteBishopSquare & lightSquares) != 0;
         var blackBishopOnLight = (blackBishopSquare & lightSquares) != 0;

         return whiteBishopOnLight == blackBishopOnLight;
      }

      return false;
   }
}
