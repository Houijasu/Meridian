namespace Meridian.Core.MoveGeneration;

using System.Runtime.CompilerServices;

/// <summary>
///    Main move generator class that coordinates all piece-specific generators.
/// </summary>
public static class MoveGenerator
{
    /// <summary>
    ///    Generates all pseudo-legal moves for the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static void GenerateMoves(ref readonly Position position, ref MoveList moveList)
   {
      var sideToMove = position.SideToMove;
      var ourPieces = position.GetColorBitboard(sideToMove);
      var theirPieces = position.GetColorBitboard(sideToMove.Flip());
      var occupancy = position.Occupancy;

      // Generate moves for each piece type
      PawnMoves.Generate(in position, ref moveList, sideToMove, ourPieces, theirPieces, occupancy);
      KnightMoves.Generate(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      BishopMoves.Generate(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      RookMoves.Generate(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      QueenMoves.Generate(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      KingMoves.Generate(in position, ref moveList, sideToMove, ourPieces, theirPieces);
   }

    /// <summary>
    ///    Generates only capture moves for quiescence search.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static void GenerateCaptures(ref readonly Position position, ref MoveList moveList)
   {
      var sideToMove = position.SideToMove;
      var ourPieces = position.GetColorBitboard(sideToMove);
      var theirPieces = position.GetColorBitboard(sideToMove.Flip());
      var occupancy = position.Occupancy;

      // Generate captures for each piece type
      PawnMoves.GenerateCaptures(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      KnightMoves.GenerateCaptures(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      BishopMoves.GenerateCaptures(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      RookMoves.GenerateCaptures(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      QueenMoves.GenerateCaptures(in position, ref moveList, sideToMove, ourPieces, theirPieces);
      KingMoves.GenerateCaptures(in position, ref moveList, sideToMove, ourPieces, theirPieces);
   }
}
