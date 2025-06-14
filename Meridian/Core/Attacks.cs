namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class Attacks
{
    private static readonly ulong[] KnightAttacks = new ulong[64];
    private static readonly ulong[] KingAttacks = new ulong[64];
    private static readonly ulong[] PawnAttacks = new ulong[2 * 64];

    static Attacks()
    {
        InitializeKnightAttacks();
        InitializeKingAttacks();
        InitializePawnAttacks();
    }

    private static void InitializeKnightAttacks()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            ulong attacks = 0;
            ulong bit = 1UL << sq;

            if ((bit & Bitboard.NotFileAB) != 0 && sq >= 10) attacks |= 1UL << (sq - 10);
            if ((bit & Bitboard.NotFileA) != 0 && sq >= 17) attacks |= 1UL << (sq - 17);
            if ((bit & Bitboard.NotFileH) != 0 && sq >= 15) attacks |= 1UL << (sq - 15);
            if ((bit & Bitboard.NotFileGH) != 0 && sq >= 6) attacks |= 1UL << (sq - 6);
            if ((bit & Bitboard.NotFileGH) != 0 && sq <= 53) attacks |= 1UL << (sq + 10);
            if ((bit & Bitboard.NotFileH) != 0 && sq <= 46) attacks |= 1UL << (sq + 17);
            if ((bit & Bitboard.NotFileA) != 0 && sq <= 48) attacks |= 1UL << (sq + 15);
            if ((bit & Bitboard.NotFileAB) != 0 && sq <= 57) attacks |= 1UL << (sq + 6);

            KnightAttacks[sq] = attacks;
        }
    }

    private static void InitializeKingAttacks()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            ulong attacks = 0;
            ulong bit = 1UL << sq;

            if ((bit & Bitboard.NotFileA) != 0) attacks |= 1UL << (sq - 1);
            if ((bit & Bitboard.NotFileH) != 0) attacks |= 1UL << (sq + 1);
            if (sq >= 8) attacks |= 1UL << (sq - 8);
            if (sq <= 55) attacks |= 1UL << (sq + 8);
            if ((bit & Bitboard.NotFileA) != 0 && sq >= 8) attacks |= 1UL << (sq - 9);
            if ((bit & Bitboard.NotFileH) != 0 && sq >= 8) attacks |= 1UL << (sq - 7);
            if ((bit & Bitboard.NotFileA) != 0 && sq <= 55) attacks |= 1UL << (sq + 7);
            if ((bit & Bitboard.NotFileH) != 0 && sq <= 55) attacks |= 1UL << (sq + 9);

            KingAttacks[sq] = attacks;
        }
    }

    private static void InitializePawnAttacks()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            ulong bit = 1UL << sq;

            // White pawn attacks
            ulong whiteAttacks = 0;
            if ((bit & Bitboard.NotFileA) != 0 && sq <= 54) whiteAttacks |= 1UL << (sq + 7);
            if ((bit & Bitboard.NotFileH) != 0 && sq <= 54) whiteAttacks |= 1UL << (sq + 9);
            PawnAttacks[sq] = whiteAttacks;

            // Black pawn attacks
            ulong blackAttacks = 0;
            if ((bit & Bitboard.NotFileA) != 0 && sq >= 9) blackAttacks |= 1UL << (sq - 9);
            if ((bit & Bitboard.NotFileH) != 0 && sq >= 7) blackAttacks |= 1UL << (sq - 7);
            PawnAttacks[64 + sq] = blackAttacks;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetKnightAttacks(Square square) => KnightAttacks[(int)square];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetKingAttacks(Square square) => KingAttacks[(int)square];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetPawnAttacks(Square square, Color color) => PawnAttacks[(int)color * 64 + (int)square];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetRookAttacks(Square square, ulong occupied) => MagicBitboards.GetRookAttacks((int)square, occupied);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetBishopAttacks(Square square, ulong occupied) => MagicBitboards.GetBishopAttacks((int)square, occupied);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetQueenAttacks(Square square, ulong occupied)
    {
        return MagicBitboards.GetQueenAttacks((int)square, occupied);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetPieceAttacks(Piece piece, Square square, ulong occupied, Color color) => piece switch
    {
        Piece.Pawn => GetPawnAttacks(square, color),
        Piece.Knight => GetKnightAttacks(square),
        Piece.Bishop => GetBishopAttacks(square, occupied),
        Piece.Rook => GetRookAttacks(square, occupied),
        Piece.Queen => GetQueenAttacks(square, occupied),
        Piece.King => GetKingAttacks(square),
        _ => 0
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetAttackers(ref BoardState board, Square square, ulong occupied)
    {
        ulong attackers = 0;

        attackers |= GetKnightAttacks(square) & (board.WhiteKnights | board.BlackKnights);
        attackers |= GetKingAttacks(square) & (board.WhiteKing | board.BlackKing);
        attackers |= GetRookAttacks(square, occupied) & (board.WhiteRooks | board.BlackRooks | board.WhiteQueens | board.BlackQueens);
        attackers |= GetBishopAttacks(square, occupied) & (board.WhiteBishops | board.BlackBishops | board.WhiteQueens | board.BlackQueens);
        attackers |= GetPawnAttacks(square, Color.Black) & board.WhitePawns;
        attackers |= GetPawnAttacks(square, Color.White) & board.BlackPawns;

        return attackers;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSquareAttacked(ref BoardState board, Square square, Color byColor)
    {
        ulong occupied = board.AllPieces;
        ulong enemyPieces = byColor == Color.White ? board.WhitePieces : board.BlackPieces;
        return (GetAttackers(ref board, square, occupied) & enemyPieces) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXRayAttacks(ref BoardState board, Square square, ulong occupied, ulong blockers)
    {
        ulong attacks = GetBishopAttacks(square, occupied);
        blockers &= attacks;
        return attacks ^ GetBishopAttacks(square, occupied ^ blockers);
    }
}