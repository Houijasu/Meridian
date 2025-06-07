namespace Meridian.Core.Evaluation;

using System.Runtime.CompilerServices;

/// <summary>
///    Static position evaluation.
/// </summary>
public static class Evaluator
{
    private static readonly PawnHashTable pawnHashTable = new(); // 16 MB pawn hash
    private static readonly EvaluationCache evalCache = new(); // 32 MB eval cache
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
      // Check evaluation cache first
      if (evalCache.Probe(position.Hash, out int cachedScore))
      {
         return cachedScore;
      }
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
      
      // Add pawn structure evaluation with caching
      var pawnHash = position.GetPawnHash();
      int pawnMgScore, pawnEgScore;
      ulong passedPawns;
      
      if (!pawnHashTable.Probe(pawnHash, out pawnMgScore, out pawnEgScore, out passedPawns))
      {
         // Not in cache, evaluate and store
         PawnStructure.EvaluateWithPhases(in position, out pawnMgScore, out pawnEgScore, out passedPawns);
         pawnHashTable.Store(pawnHash, pawnMgScore, pawnEgScore, passedPawns);
      }
      
      mgScore += pawnMgScore;
      
      // Add king safety evaluation (primarily middlegame)
      var kingSafetyScore = KingSafety.Evaluate(in position);
      mgScore += kingSafetyScore;
      
      // Add mobility evaluation
      var mobilityScore = Mobility.Evaluate(in position);
      mgScore += mobilityScore;
      
      // Endgame evaluation components
      int egScore = materialScore + positionalScore + pawnEgScore;
      
      // Add endgame-specific evaluation
      var endgameScore = Endgame.Evaluate(in position);
      egScore += endgameScore;
      
      // Add reduced mobility in endgame
      egScore += mobilityScore / 2;
      
      // Tapered evaluation
      int finalScore = (mgScore * middlegamePhase + egScore * endgamePhase) / 256;
      
      // Store in cache
      evalCache.Store(position.Hash, finalScore);
      
      return finalScore;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static int EvaluatePieces(ulong bitboard, PieceType pieceType, Color color, ref int positionalScore, in Position position)
   {
      if (bitboard == 0) return 0;

      var count = Bitboard.PopCount(bitboard);
      var material = count * PieceValues.GetValue(pieceType);

      // Determine if we're in endgame (simplified: queens off or very few pieces)
      var isEndgame = position is { WhiteQueens: 0, BlackQueens: 0 };

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
      {
         return false;
      }

      var whiteKnights = Bitboard.PopCount(position.WhiteKnights);
      var whiteBishops = Bitboard.PopCount(position.WhiteBishops);
      var blackKnights = Bitboard.PopCount(position.BlackKnights);
      var blackBishops = Bitboard.PopCount(position.BlackBishops);

      var whiteMinorPieces = whiteKnights + whiteBishops;
      var blackMinorPieces = blackKnights + blackBishops;

      return (whiteMinorPieces, blackMinorPieces) switch
      {
         // King vs King
         (0, 0) => true,
         // King and minor piece vs King
         (1, 0) => true,
         (0, 1) => true,
         // King and two knights vs King (cannot force mate)
         (2, 0) when whiteKnights == 2 => true,
         (0, 2) when blackKnights == 2 => true,
         // Both sides have only one bishop on same color squares
         (1, 1) when whiteBishops == 1 && blackBishops == 1 => AreBishopsOnSameColor(in position),
         _ => false
      };
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static bool AreBishopsOnSameColor(in Position position)
   {
      // This assumes there's exactly one white bishop and one black bishop.
      var whiteBishopSquareBitboard = position.WhiteBishops;
      var blackBishopSquareBitboard = position.BlackBishops;

      // If for some reason a bitboard is empty (e.g. called incorrectly), it's not "same color" in a meaningful way.
      if (whiteBishopSquareBitboard == 0 || blackBishopSquareBitboard == 0)
          return false;

      var whiteBishopSquare = Bitboard.GetLsb(whiteBishopSquareBitboard);
      var blackBishopSquare = Bitboard.GetLsb(blackBishopSquareBitboard);

      // Light squares have (file + rank) % 2 == 0. Dark squares have (file + rank) % 2 != 0.
      // Or, more efficiently, check the LSB of (file + rank).
      // A square is light if (square % 8 + square / 8) % 2 == 0
      // A square is light if ((square & 7) + (square >> 3)) % 2 == 0
      var whiteBishopIsLight = (((whiteBishopSquare & 7) + (whiteBishopSquare >> 3)) & 1) == 0;
      var blackBishopIsLight = (((blackBishopSquare & 7) + (blackBishopSquare >> 3)) & 1) == 0;

      return whiteBishopIsLight == blackBishopIsLight;
   }
}
