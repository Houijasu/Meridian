namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class Evaluation
{
    // Piece values in centipawns (1 pawn = 100)
    private static readonly int[] PieceValues = [
        0,    // None
        100,  // Pawn
        320,  // Knight
        330,  // Bishop
        500,  // Rook
        900,  // Queen
        20000 // King
    ];

    // Piece-square tables for positional evaluation (from white's perspective)
    // These values encourage pieces to occupy good squares
    private static readonly int[] PawnSquareTable = [
        0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 25, 25, 10,  5,  5,
         0,  0,  0, 20, 20,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-20,-20, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    ];

    private static readonly int[] KnightSquareTable = [
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50
    ];

    private static readonly int[] BishopSquareTable = [
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -20,-10,-10,-10,-10,-10,-10,-20
    ];

    private static readonly int[] RookSquareTable = [
        0,  0,  0,  0,  0,  0,  0,  0,
         5, 10, 10, 10, 10, 10, 10,  5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
         0,  0,  0,  5,  5,  0,  0,  0
    ];

    private static readonly int[] QueenSquareTable = [
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
         -5,  0,  5,  5,  5,  5,  0, -5,
          0,  0,  5,  5,  5,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20
    ];

    private static readonly int[] KingMiddleGameTable = [
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,  0,  0,  0,  0, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20
    ];

    private static readonly int[] KingEndGameTable = [
        -50,-40,-30,-20,-20,-30,-40,-50,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -50,-30,-30,-30,-30,-30,-30,-50
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Evaluate(ref BoardState board)
    {
        int score = 0;
        
        // Material and position evaluation
        score += EvaluatePieces(ref board, Color.White);
        score -= EvaluatePieces(ref board, Color.Black);
        
        // Add small random factor to avoid repetitions
        score += board.FullMoveNumber * 7 % 10;
        
        // Return score from the perspective of the side to move
        return board.SideToMove == Color.White ? score : -score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluatePieces(ref BoardState board, Color color)
    {
        int score = 0;
        bool isWhite = color == Color.White;
        
        // Count material
        int totalMaterial = 0;
        
        // Pawns
        ulong pawns = isWhite ? board.WhitePawns : board.BlackPawns;
        int pawnCount = Bitboard.PopCount(pawns);
        score += pawnCount * PieceValues[(int)Piece.Pawn];
        totalMaterial += pawnCount * PieceValues[(int)Piece.Pawn];
        
        // Add positional value for pawns
        while (pawns != 0)
        {
            int sq = Bitboard.PopLsb(ref pawns);
            int psq = isWhite ? sq : sq ^ 56; // Mirror for black
            score += PawnSquareTable[psq];
        }
        
        // Knights
        ulong knights = isWhite ? board.WhiteKnights : board.BlackKnights;
        int knightCount = Bitboard.PopCount(knights);
        score += knightCount * PieceValues[(int)Piece.Knight];
        totalMaterial += knightCount * PieceValues[(int)Piece.Knight];
        
        while (knights != 0)
        {
            int sq = Bitboard.PopLsb(ref knights);
            int psq = isWhite ? sq : sq ^ 56;
            score += KnightSquareTable[psq];
        }
        
        // Bishops
        ulong bishops = isWhite ? board.WhiteBishops : board.BlackBishops;
        int bishopCount = Bitboard.PopCount(bishops);
        score += bishopCount * PieceValues[(int)Piece.Bishop];
        totalMaterial += bishopCount * PieceValues[(int)Piece.Bishop];
        
        // Bishop pair bonus
        if (bishopCount >= 2)
            score += 30;
        
        while (bishops != 0)
        {
            int sq = Bitboard.PopLsb(ref bishops);
            int psq = isWhite ? sq : sq ^ 56;
            score += BishopSquareTable[psq];
        }
        
        // Rooks
        ulong rooks = isWhite ? board.WhiteRooks : board.BlackRooks;
        int rookCount = Bitboard.PopCount(rooks);
        score += rookCount * PieceValues[(int)Piece.Rook];
        totalMaterial += rookCount * PieceValues[(int)Piece.Rook];
        
        while (rooks != 0)
        {
            int sq = Bitboard.PopLsb(ref rooks);
            int psq = isWhite ? sq : sq ^ 56;
            score += RookSquareTable[psq];
        }
        
        // Queens
        ulong queens = isWhite ? board.WhiteQueens : board.BlackQueens;
        int queenCount = Bitboard.PopCount(queens);
        score += queenCount * PieceValues[(int)Piece.Queen];
        totalMaterial += queenCount * PieceValues[(int)Piece.Queen];
        
        while (queens != 0)
        {
            int sq = Bitboard.PopLsb(ref queens);
            int psq = isWhite ? sq : sq ^ 56;
            score += QueenSquareTable[psq];
        }
        
        // King (positional only)
        ulong king = isWhite ? board.WhiteKing : board.BlackKing;
        if (king != 0)
        {
            int sq = Bitboard.BitScanForward(king);
            int psq = isWhite ? sq : sq ^ 56;
            
            // Use endgame table if low material
            if (totalMaterial < 1500)
                score += KingEndGameTable[psq];
            else
                score += KingMiddleGameTable[psq];
        }
        
        return score;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEndgame(ref BoardState board)
    {
        // Simple endgame detection based on material
        int whiteMaterial = 
            Bitboard.PopCount(board.WhiteQueens) * PieceValues[(int)Piece.Queen] +
            Bitboard.PopCount(board.WhiteRooks) * PieceValues[(int)Piece.Rook] +
            Bitboard.PopCount(board.WhiteBishops) * PieceValues[(int)Piece.Bishop] +
            Bitboard.PopCount(board.WhiteKnights) * PieceValues[(int)Piece.Knight];
            
        int blackMaterial = 
            Bitboard.PopCount(board.BlackQueens) * PieceValues[(int)Piece.Queen] +
            Bitboard.PopCount(board.BlackRooks) * PieceValues[(int)Piece.Rook] +
            Bitboard.PopCount(board.BlackBishops) * PieceValues[(int)Piece.Bishop] +
            Bitboard.PopCount(board.BlackKnights) * PieceValues[(int)Piece.Knight];
            
        return (whiteMaterial + blackMaterial) < 1500;
    }
}