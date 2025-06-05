namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
///    A stack-allocated list for storing moves during generation.
///    Avoids heap allocations for better performance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public ref struct MoveList
{
   private Span<Move> _moves;

   /// <summary>
   ///    Creates a new move list with the given buffer.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public MoveList(Span<Move> buffer)
   {
      _moves = buffer;
      Count = 0;
   }

   /// <summary>
   ///    Gets the number of moves in the list.
   /// </summary>
   public int Count { get; private set; }

   /// <summary>
   ///    Gets the moves as a span.
   /// </summary>
   public readonly ReadOnlySpan<Move> Moves => _moves[..Count];

   /// <summary>
   ///    Adds a move to the list.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void Add(Move move) => _moves[Count++] = move;

   /// <summary>
   ///    Adds a quiet move to the list.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void AddQuiet(Square from, Square to, Piece piece) => _moves[Count++] = Move.CreateQuiet(from, to, piece);

   /// <summary>
   ///    Adds a capture move to the list.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void AddCapture(Square from, Square to, Piece piece, Piece captured) => _moves[Count++] = Move.CreateCapture(from, to, piece, captured);

   /// <summary>
   ///    Adds promotion moves (all four types) to the list.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void AddPromotions(Square from, Square to, Piece piece, Piece captured = Piece.None)
   {
      _moves[Count++] = Move.CreatePromotion(from, to, piece, PieceType.Queen, captured);
      _moves[Count++] = Move.CreatePromotion(from, to, piece, PieceType.Rook, captured);
      _moves[Count++] = Move.CreatePromotion(from, to, piece, PieceType.Bishop, captured);
      _moves[Count++] = Move.CreatePromotion(from, to, piece, PieceType.Knight, captured);
   }

   /// <summary>
   ///    Clears the move list.
   /// </summary>
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void Clear() => Count = 0;

   /// <summary>
   ///    Gets a move at the specified index.
   /// </summary>
   public readonly Move this[int index] => _moves[index];
}

/// <summary>
///    Static class for creating move lists with appropriate buffer sizes.
/// </summary>
public static class MoveListFactory
{
    /// <summary>
    ///    Maximum possible moves in any chess position.
    /// </summary>
    public const int MaxMoves = 256;

    /// <summary>
    ///    Creates a move list with stack-allocated buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static MoveList Create(Span<Move> buffer) => new(buffer);
}
