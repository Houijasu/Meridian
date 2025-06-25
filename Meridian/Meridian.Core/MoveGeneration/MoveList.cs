#nullable enable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Meridian.Core.Board;

namespace Meridian.Core.MoveGeneration;

[StructLayout(LayoutKind.Sequential)]
public ref struct MoveList
{
    private const int MaxMoves = 218;
    private Span<Move> _moves;
    private int _count;

    public MoveList(Span<Move> buffer)
    {
        _moves = buffer;
        _count = 0;
    }

    public readonly int Count => _count;

    public readonly Move this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange();
            return _moves[index];
        }
    }
    
    public void Set(int index, Move move)
    {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange();
        _moves[index] = move;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Move move)
    {
        if (_count >= _moves.Length)
            ThrowCapacityExceeded();
        _moves[_count++] = move;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Square from, Square to, MoveType flags = MoveType.None, 
                    Piece captured = Piece.None, PieceType promotion = PieceType.None)
    {
        Add(new Move(from, to, flags, captured, promotion));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddQuiet(Square from, Square to)
    {
        Add(new Move(from, to));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddCapture(Square from, Square to, Piece captured)
    {
        Add(new Move(from, to, MoveType.Capture, captured));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddPromotions(Square from, Square to, MoveType baseFlags, Piece captured = Piece.None)
    {
        var flags = baseFlags | MoveType.Promotion;
        Add(new Move(from, to, flags, captured, PieceType.Queen));
        Add(new Move(from, to, flags, captured, PieceType.Rook));
        Add(new Move(from, to, flags, captured, PieceType.Bishop));
        Add(new Move(from, to, flags, captured, PieceType.Knight));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
    }

    public readonly Span<Move> AsSpan() => _moves[.._count];

    public readonly Span<Move>.Enumerator GetEnumerator() => AsSpan().GetEnumerator();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange() => throw new IndexOutOfRangeException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityExceeded() => throw new InvalidOperationException("Move list capacity exceeded");

    public static void CreateBuffer(out Span<Move> buffer)
    {
        buffer = new Move[MaxMoves];
    }

    public static unsafe void CreateStackBuffer(Move* buffer, out Span<Move> span)
    {
        span = new Span<Move>(buffer, MaxMoves);
    }
}