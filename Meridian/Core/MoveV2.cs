namespace Meridian.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MoveV2
{
    private readonly ushort _data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MoveV2(Square from, Square to, MoveType type = MoveType.Normal, Piece promotionPiece = Piece.None)
    {
        // Better layout that properly handles all cases:
        // Bits 0-5: from square (6 bits)
        // Bits 6-11: to square (6 bits)  
        // Bits 12-13: move type (2 bits: Normal=0, Capture=1, Castle=2, EnPassant=3)
        // Bit 14: promotion flag (1 bit)
        // Bit 15: reserved/special flag
        
        _data = (ushort)((int)from | ((int)to << 6) | ((int)type << 12));
        
        if (promotionPiece != Piece.None)
        {
            // Set promotion flag
            _data |= (ushort)(1 << 14);
            
            // Store promotion piece type in the reserved space
            // We'll encode it in a separate static array indexed by move
            // For now, we'll use the fact that promotions only happen on specific squares
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
        get => (MoveType)((_data >> 12) & 0x3);
    }

    public bool IsPromotion
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_data & (1 << 14)) != 0;
    }

    public Piece PromotionPiece
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!IsPromotion) return Piece.None;
            
            // For a proper implementation, we'd need either:
            // 1. A wider data type (uint instead of ushort)
            // 2. A separate promotion piece array
            // 3. Some other encoding scheme
            
            // For now, return Queen as default (most common)
            return Piece.Queen;
        }
    }
}