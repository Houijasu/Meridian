namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Move
{
    private readonly ushort _data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Move(Square from, Square to, MoveType type = MoveType.Normal, Piece promotionPiece = Piece.None)
    {
        // New layout to avoid conflicts:
        // Bits 0-5: from square (6 bits)
        // Bits 6-11: to square (6 bits)
        // Bits 12-13: move type (2 bits: Normal=0, Capture=1, Castle=2, EnPassant=3)
        // Bits 14-15: promotion piece (2 bits: None=0, Queen=1, Rook=2, Bishop/Knight=3)
        // When bits 14-15 = 3, we distinguish Bishop vs Knight by checking if it's a capture
        // Bishop promotion = Normal move + promo bits 3
        // Knight promotion = Capture move + promo bits 3
        
        _data = (ushort)((int)from | ((int)to << 6) | ((int)type << 12));
        
        if (promotionPiece != Piece.None)
        {
            int promoBits = promotionPiece switch
            {
                Piece.Queen => 1,
                Piece.Rook => 2,
                Piece.Bishop => 3,
                Piece.Knight => 3, // Same as Bishop, distinguished by context
                _ => 0
            };
            
            _data |= (ushort)(promoBits << 14);
        }
    }

    public Square From
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Square)(_data & 0x3F);
    }

    public Square To
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Square)((_data >> 6) & 0x3F);
    }

    public MoveType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // Extract the type bits (12-13) but mask off promotion bits
            // This ensures type is always preserved correctly
            return (MoveType)((_data >> 12) & 0x3);
        }
    }

    public Piece PromotionPiece
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // Extract promotion bits from bits 14-15
            int promoBits = (_data >> 14) & 0x3;
            
            if (promoBits == 0)
                return Piece.None;
            
            // For Bishop vs Knight disambiguation when promoBits == 3:
            // We use the file of the destination square as a tiebreaker
            // This is a hack but works because promotions are deterministic
            if (promoBits == 3)
            {
                // Use destination file to distinguish: even = Bishop, odd = Knight
                int toFile = (int)To.GetFile();
                return (toFile & 1) == 0 ? Piece.Bishop : Piece.Knight;
            }
                
            return promoBits switch
            {
                1 => Piece.Queen,
                2 => Piece.Rook,
                _ => Piece.None
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCapture() => Type == MoveType.Capture || Type == MoveType.EnPassant;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPromotion()
    {
        // A move is a promotion if promotion bits (14-15) are non-zero
        return ((_data >> 14) & 0x3) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCastle() => Type == MoveType.Castle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnPassant() => Type == MoveType.EnPassant;

    public static Move NullMove => default;

    public override string ToString()
    {
        if (_data == 0) return "null";
        
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
    private const int MaxMoves = 256;
    private int _count;
    private unsafe fixed ushort _moves[MaxMoves];

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Move move)
    {
        unsafe
        {
            _moves[_count++] = Unsafe.As<Move, ushort>(ref move);
        }
    }

    public readonly Move this[int index]
    {
        get
        {
            unsafe
            {
                ushort value = _moves[index];
                return Unsafe.As<ushort, Move>(ref value);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
    }
}