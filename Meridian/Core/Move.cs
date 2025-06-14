namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Move
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Move(Square from, Square to, MoveType type = MoveType.Normal, Piece promotionPiece = Piece.None)
    {
        // Layout (low to high bits):
        // 0-5   : from square
        // 6-11  : to square
        // 12-13 : move type (Normal=0, Capture=1, Castle=2, EnPassant=3)
        // 14-16 : promotion piece (0=None, 1=Queen, 2=Rook, 3=Bishop, 4=Knight)

        Data = (uint)from | ((uint)to << 6) | ((uint)type << 12) | ((uint)promotionPiece << 14);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Move(uint data)
    {
        Data = data;
    }

    public uint Data
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public Square From
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Square)(Data & 0x3F);
    }

    public Square To
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Square)((Data >> 6) & 0x3F);
    }

    public MoveType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (MoveType)((Data >> 12) & 0x3);
    }

    public Piece PromotionPiece
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Piece)((Data >> 14) & 0x7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCapture() => Type == MoveType.Capture || Type == MoveType.EnPassant;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPromotion()
    {
        // A move is a promotion if the promotion piece field is non-zero
        return ((Data >> 14) & 0x7) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCastle() => Type == MoveType.Castle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnPassant() => Type == MoveType.EnPassant;

    public static Move NullMove => default;

    public override string ToString()
    {
        if (Data == 0) return "null";
        
        char[] notation = new char[5];
        int index = 0;
        
        notation[index++] = (char)('a' + (int)From.GetFile());
        notation[index++] = (char)('1' + (int)From.GetRank());
        notation[index++] = (char)('a' + (int)To.GetFile());
        notation[index++] = (char)('1' + (int)To.GetRank());
        
        var promoPiece = PromotionPiece;
        if (promoPiece != Piece.None)
        {
            notation[index++] = promoPiece switch
            {
                Piece.Queen => 'q',
                Piece.Rook => 'r',
                Piece.Bishop => 'b',
                Piece.Knight => 'n',
                _ => ' '
            };
        }
        
        return new string(notation, 0, index);
    }
}

public enum MoveType : byte
{
    Normal = 0,
    Capture = 1,
    Castle = 2,
    EnPassant = 3
    // Promotions are detected by checking if promotion piece != None
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public ref struct MoveList
{
    public const int MaxMoves = 256;
    private unsafe fixed uint _moves[MaxMoves];

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Move move)
    {
        if (Count >= MaxMoves)
            throw new InvalidOperationException($"MoveList overflow: trying to add move {Count + 1} but max is {MaxMoves}");
            
        unsafe
        {
            _moves[Count++] = Unsafe.As<Move, uint>(ref move);
        }
    }

    public readonly Move this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException($"Index {index} is out of range. Count is {Count}");
                
            unsafe
            {
                uint value = _moves[index];
                return Unsafe.As<uint, Move>(ref value);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly unsafe Span<Move> AsSpan()
    {
        fixed (uint* ptr = _moves)
        {
            return new Span<Move>((Move*)ptr, Count);
        }
    }
}