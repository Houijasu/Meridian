#nullable enable

using System.Runtime.CompilerServices;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;

namespace Meridian.Core.Evaluation;

public static class Evaluator
{
    private const int PawnValue = 100;
    private const int KnightValue = 320;
    private const int BishopValue = 330;
    private const int RookValue = 500;
    private const int QueenValue = 900;
    
    private const int MidgameLimit = 15258;
    private const int EndgameLimit = 3915;
    
    public static int Evaluate(Position position)
    {
        if (position == null) return 0;
        var score = 0;
        var midgameScore = 0;
        var endgameScore = 0;
        
        var whiteMaterial = CountMaterial(position, Color.White);
        var blackMaterial = CountMaterial(position, Color.Black);
        var totalMaterial = whiteMaterial + blackMaterial;
        
        var phase = CalculatePhase(totalMaterial);
        
        midgameScore += EvaluateMaterial(position, Color.White) - EvaluateMaterial(position, Color.Black);
        endgameScore += EvaluateMaterial(position, Color.White) - EvaluateMaterial(position, Color.Black);
        
        midgameScore += EvaluatePieceSquareTables(position, Color.White, true) - 
                       EvaluatePieceSquareTables(position, Color.Black, true);
        endgameScore += EvaluatePieceSquareTables(position, Color.White, false) - 
                       EvaluatePieceSquareTables(position, Color.Black, false);
        
        var pawnStructure = EvaluatePawnStructure(position);
        midgameScore += pawnStructure.MidgameScore;
        endgameScore += pawnStructure.EndgameScore;
        
        midgameScore += EvaluateKingSafety(position, Color.White) - EvaluateKingSafety(position, Color.Black);
        
        midgameScore += EvaluateMobility(position, Color.White) - EvaluateMobility(position, Color.Black);
        endgameScore += EvaluateMobility(position, Color.White) - EvaluateMobility(position, Color.Black);
        
        score = (midgameScore * phase + endgameScore * (256 - phase)) / 256;
        
        return position.SideToMove == Color.White ? score : -score;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculatePhase(int totalMaterial)
    {
        if (totalMaterial > MidgameLimit) return 256;
        if (totalMaterial < EndgameLimit) return 0;
        return ((totalMaterial - EndgameLimit) * 256) / (MidgameLimit - EndgameLimit);
    }
    
    private static int CountMaterial(Position position, Color color)
    {
        var material = 0;
        material += Bitboard.PopCount(position.GetBitboard(color, PieceType.Pawn)) * PawnValue;
        material += Bitboard.PopCount(position.GetBitboard(color, PieceType.Knight)) * KnightValue;
        material += Bitboard.PopCount(position.GetBitboard(color, PieceType.Bishop)) * BishopValue;
        material += Bitboard.PopCount(position.GetBitboard(color, PieceType.Rook)) * RookValue;
        material += Bitboard.PopCount(position.GetBitboard(color, PieceType.Queen)) * QueenValue;
        return material;
    }
    
    private static int EvaluateMaterial(Position position, Color color)
    {
        var material = 0;
        
        var pawns = Bitboard.PopCount(position.GetBitboard(color, PieceType.Pawn));
        var knights = Bitboard.PopCount(position.GetBitboard(color, PieceType.Knight));
        var bishops = Bitboard.PopCount(position.GetBitboard(color, PieceType.Bishop));
        var rooks = Bitboard.PopCount(position.GetBitboard(color, PieceType.Rook));
        var queens = Bitboard.PopCount(position.GetBitboard(color, PieceType.Queen));
        
        material += pawns * PawnValue;
        material += knights * KnightValue;
        material += bishops * BishopValue;
        material += rooks * RookValue;
        material += queens * QueenValue;
        
        if (bishops >= 2) material += 30;
        
        if (knights > 0 && pawns >= 5) material += knights * 10;
        if (bishops > 0 && pawns <= 4) material += bishops * 10;
        
        return material;
    }
    
    private static int EvaluatePieceSquareTables(Position position, Color color, bool midgame)
    {
        var score = 0;
        var isWhite = color == Color.White;
        
        var pawns = position.GetBitboard(color, PieceType.Pawn);
        while (pawns.IsNotEmpty())
        {
            pawns = pawns.PopLsb(out var sq);
            var square = (Square)sq;
            score += GetPieceSquareValue(PieceType.Pawn, square, isWhite, midgame);
        }
        
        var knights = position.GetBitboard(color, PieceType.Knight);
        while (knights.IsNotEmpty())
        {
            knights = knights.PopLsb(out var sq);
            var square = (Square)sq;
            score += GetPieceSquareValue(PieceType.Knight, square, isWhite, midgame);
        }
        
        var bishops = position.GetBitboard(color, PieceType.Bishop);
        while (bishops.IsNotEmpty())
        {
            bishops = bishops.PopLsb(out var sq);
            var square = (Square)sq;
            score += GetPieceSquareValue(PieceType.Bishop, square, isWhite, midgame);
        }
        
        var rooks = position.GetBitboard(color, PieceType.Rook);
        while (rooks.IsNotEmpty())
        {
            rooks = rooks.PopLsb(out var sq);
            var square = (Square)sq;
            score += GetPieceSquareValue(PieceType.Rook, square, isWhite, midgame);
        }
        
        var queens = position.GetBitboard(color, PieceType.Queen);
        while (queens.IsNotEmpty())
        {
            queens = queens.PopLsb(out var sq);
            var square = (Square)sq;
            score += GetPieceSquareValue(PieceType.Queen, square, isWhite, midgame);
        }
        
        var king = position.GetBitboard(color, PieceType.King);
        if (king.IsNotEmpty())
        {
            var square = (Square)king.GetLsbIndex();
            score += GetPieceSquareValue(PieceType.King, square, isWhite, midgame);
        }
        
        return score;
    }
    
    private static int GetPieceSquareValue(PieceType pieceType, Square square, bool isWhite, bool midgame)
    {
        var index = isWhite ? (int)square : (int)square ^ 56;
        
        return pieceType switch
        {
            PieceType.Pawn => midgame ? PawnTableMidgame[index] : PawnTableEndgame[index],
            PieceType.Knight => midgame ? KnightTableMidgame[index] : KnightTableEndgame[index],
            PieceType.Bishop => midgame ? BishopTableMidgame[index] : BishopTableEndgame[index],
            PieceType.Rook => midgame ? RookTableMidgame[index] : RookTableEndgame[index],
            PieceType.Queen => midgame ? QueenTableMidgame[index] : QueenTableEndgame[index],
            PieceType.King => midgame ? KingTableMidgame[index] : KingTableEndgame[index],
            _ => 0
        };
    }
    
    private static readonly int[] PawnTableMidgame = 
    {
          0,   0,   0,   0,   0,   0,   0,   0,
         50,  50,  50,  50,  50,  50,  50,  50,
         10,  10,  20,  30,  30,  20,  10,  10,
          5,   5,  10,  25,  25,  10,   5,   5,
          0,   0,   0,  20,  20,   0,   0,   0,
          5,  -5, -10,   0,   0, -10,  -5,   5,
          5,  10,  10, -20, -20,  10,  10,   5,
          0,   0,   0,   0,   0,   0,   0,   0
    };
    
    private static readonly int[] PawnTableEndgame = 
    {
          0,   0,   0,   0,   0,   0,   0,   0,
         90,  90,  90,  90,  90,  90,  90,  90,
         50,  50,  50,  50,  50,  50,  50,  50,
         30,  30,  30,  30,  30,  30,  30,  30,
         20,  20,  20,  20,  20,  20,  20,  20,
         10,  10,  10,  10,  10,  10,  10,  10,
         10,  10,  10,  10,  10,  10,  10,  10,
          0,   0,   0,   0,   0,   0,   0,   0
    };
    
    private static readonly int[] KnightTableMidgame = 
    {
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20,   0,   0,   0,   0, -20, -40,
        -30,   0,  10,  15,  15,  10,   0, -30,
        -30,   5,  15,  20,  20,  15,   5, -30,
        -30,   0,  15,  20,  20,  15,   0, -30,
        -30,   5,  10,  15,  15,  10,   5, -30,
        -40, -20,   0,   5,   5,   0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50
    };
    
    private static readonly int[] KnightTableEndgame = 
    {
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20,   0,   0,   0,   0, -20, -40,
        -30,   0,  10,  15,  15,  10,   0, -30,
        -30,   5,  15,  20,  20,  15,   5, -30,
        -30,   0,  15,  20,  20,  15,   0, -30,
        -30,   5,  10,  15,  15,  10,   5, -30,
        -40, -20,   0,   5,   5,   0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50
    };
    
    private static readonly int[] BishopTableMidgame = 
    {
        -20, -10, -10, -10, -10, -10, -10, -20,
        -10,   0,   0,   0,   0,   0,   0, -10,
        -10,   0,   5,  10,  10,   5,   0, -10,
        -10,   5,   5,  10,  10,   5,   5, -10,
        -10,   0,  10,  10,  10,  10,   0, -10,
        -10,  10,  10,  10,  10,  10,  10, -10,
        -10,   5,   0,   0,   0,   0,   5, -10,
        -20, -10, -10, -10, -10, -10, -10, -20
    };
    
    private static readonly int[] BishopTableEndgame = 
    {
        -20, -10, -10, -10, -10, -10, -10, -20,
        -10,   0,   0,   0,   0,   0,   0, -10,
        -10,   0,   5,  10,  10,   5,   0, -10,
        -10,   5,   5,  10,  10,   5,   5, -10,
        -10,   0,  10,  10,  10,  10,   0, -10,
        -10,  10,  10,  10,  10,  10,  10, -10,
        -10,   5,   0,   0,   0,   0,   5, -10,
        -20, -10, -10, -10, -10, -10, -10, -20
    };
    
    private static readonly int[] RookTableMidgame = 
    {
          0,   0,   0,   0,   0,   0,   0,   0,
          5,  10,  10,  10,  10,  10,  10,   5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
          0,   0,   0,   5,   5,   0,   0,   0
    };
    
    private static readonly int[] RookTableEndgame = 
    {
          0,   0,   0,   0,   0,   0,   0,   0,
          5,  10,  10,  10,  10,  10,  10,   5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
         -5,   0,   0,   0,   0,   0,   0,  -5,
          0,   0,   0,   5,   5,   0,   0,   0
    };
    
    private static readonly int[] QueenTableMidgame = 
    {
        -20, -10, -10,  -5,  -5, -10, -10, -20,
        -10,   0,   0,   0,   0,   0,   0, -10,
        -10,   0,   5,   5,   5,   5,   0, -10,
         -5,   0,   5,   5,   5,   5,   0,  -5,
          0,   0,   5,   5,   5,   5,   0,  -5,
        -10,   5,   5,   5,   5,   5,   0, -10,
        -10,   0,   5,   0,   0,   0,   0, -10,
        -20, -10, -10,  -5,  -5, -10, -10, -20
    };
    
    private static readonly int[] QueenTableEndgame = 
    {
        -20, -10, -10,  -5,  -5, -10, -10, -20,
        -10,   0,   0,   0,   0,   0,   0, -10,
        -10,   0,   5,   5,   5,   5,   0, -10,
         -5,   0,   5,   5,   5,   5,   0,  -5,
          0,   0,   5,   5,   5,   5,   0,  -5,
        -10,   5,   5,   5,   5,   5,   0, -10,
        -10,   0,   5,   0,   0,   0,   0, -10,
        -20, -10, -10,  -5,  -5, -10, -10, -20
    };
    
    private static readonly int[] KingTableMidgame = 
    {
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -10, -20, -20, -20, -20, -20, -20, -10,
         20,  20,   0,   0,   0,   0,  20,  20,
         20,  30,  10,   0,   0,  10,  30,  20
    };
    
    private static readonly int[] KingTableEndgame = 
    {
        -50, -40, -30, -20, -20, -30, -40, -50,
        -30, -20, -10,   0,   0, -10, -20, -30,
        -30, -10,  20,  30,  30,  20, -10, -30,
        -30, -10,  30,  40,  40,  30, -10, -30,
        -30, -10,  30,  40,  40,  30, -10, -30,
        -30, -10,  20,  30,  30,  20, -10, -30,
        -30, -30,   0,   0,   0,   0, -30, -30,
        -50, -30, -30, -30, -30, -30, -30, -50
    };
    
    private static (int MidgameScore, int EndgameScore) EvaluatePawnStructure(Position position)
    {
        var midgameScore = 0;
        var endgameScore = 0;
        
        var whitePawns = position.GetBitboard(Color.White, PieceType.Pawn);
        var blackPawns = position.GetBitboard(Color.Black, PieceType.Pawn);
        
        var whitePassedPawns = GetPassedPawns(whitePawns, blackPawns, Color.White);
        var blackPassedPawns = GetPassedPawns(blackPawns, whitePawns, Color.Black);
        
        midgameScore += EvaluatePassedPawns(whitePassedPawns, Color.White, true) -
                       EvaluatePassedPawns(blackPassedPawns, Color.Black, true);
        endgameScore += EvaluatePassedPawns(whitePassedPawns, Color.White, false) -
                       EvaluatePassedPawns(blackPassedPawns, Color.Black, false);
        
        var whiteDoubledPawns = CountDoubledPawns(whitePawns);
        var blackDoubledPawns = CountDoubledPawns(blackPawns);
        midgameScore += (blackDoubledPawns - whiteDoubledPawns) * 20;
        endgameScore += (blackDoubledPawns - whiteDoubledPawns) * 30;
        
        var whiteIsolatedPawns = CountIsolatedPawns(whitePawns);
        var blackIsolatedPawns = CountIsolatedPawns(blackPawns);
        midgameScore += (blackIsolatedPawns - whiteIsolatedPawns) * 15;
        endgameScore += (blackIsolatedPawns - whiteIsolatedPawns) * 20;
        
        return (midgameScore, endgameScore);
    }
    
    private static Bitboard GetPassedPawns(Bitboard ourPawns, Bitboard theirPawns, Color color)
    {
        var passedPawns = Bitboard.Empty;
        var pawns = ourPawns;
        
        while (pawns.IsNotEmpty())
        {
            pawns = pawns.PopLsb(out var sq);
            var square = (Square)sq;
            var file = square.File();
            var rank = square.Rank();
            
            var blockingMask = color == Color.White
                ? GetWhitePassedPawnMask(square)
                : GetBlackPassedPawnMask(square);
                
            if ((theirPawns & blockingMask).IsEmpty())
            {
                passedPawns |= square.ToBitboard();
            }
        }
        
        return passedPawns;
    }
    
    private static Bitboard GetWhitePassedPawnMask(Square square)
    {
        var mask = Bitboard.Empty;
        var file = square.File();
        var rank = square.Rank();
        
        for (var r = rank + 1; r <= 7; r++)
        {
            if (file > 0) mask |= SquareExtensions.FromFileRank(file - 1, r).ToBitboard();
            mask |= SquareExtensions.FromFileRank(file, r).ToBitboard();
            if (file < 7) mask |= SquareExtensions.FromFileRank(file + 1, r).ToBitboard();
        }
        
        return mask;
    }
    
    private static Bitboard GetBlackPassedPawnMask(Square square)
    {
        var mask = Bitboard.Empty;
        var file = square.File();
        var rank = square.Rank();
        
        for (var r = rank - 1; r >= 0; r--)
        {
            if (file > 0) mask |= SquareExtensions.FromFileRank(file - 1, r).ToBitboard();
            mask |= SquareExtensions.FromFileRank(file, r).ToBitboard();
            if (file < 7) mask |= SquareExtensions.FromFileRank(file + 1, r).ToBitboard();
        }
        
        return mask;
    }
    
    private static int EvaluatePassedPawns(Bitboard passedPawns, Color color, bool midgame)
    {
        var score = 0;
        var isWhite = color == Color.White;
        
        while (passedPawns.IsNotEmpty())
        {
            passedPawns = passedPawns.PopLsb(out var sq);
            var square = (Square)sq;
            var rank = isWhite ? square.Rank() : 7 - square.Rank();
            
            var bonus = midgame ? PassedPawnBonusMidgame[rank] : PassedPawnBonusEndgame[rank];
            score += bonus;
        }
        
        return score;
    }
    
    private static readonly int[] PassedPawnBonusMidgame = { 0, 5, 10, 20, 35, 60, 100, 200 };
    private static readonly int[] PassedPawnBonusEndgame = { 0, 10, 20, 40, 70, 120, 200, 300 };
    
    private static int CountDoubledPawns(Bitboard pawns)
    {
        var doubled = 0;
        
        for (var file = 0; file < 8; file++)
        {
            var fileMask = GetFileMask(file);
            var pawnsOnFile = Bitboard.PopCount(pawns & fileMask);
            if (pawnsOnFile > 1)
                doubled += pawnsOnFile - 1;
        }
        
        return doubled;
    }
    
    private static int CountIsolatedPawns(Bitboard pawns)
    {
        var isolated = 0;
        
        for (var file = 0; file < 8; file++)
        {
            var fileMask = GetFileMask(file);
            if ((pawns & fileMask).IsNotEmpty())
            {
                var adjacentFiles = Bitboard.Empty;
                if (file > 0) adjacentFiles |= GetFileMask(file - 1);
                if (file < 7) adjacentFiles |= GetFileMask(file + 1);
                
                if ((pawns & adjacentFiles).IsEmpty())
                    isolated++;
            }
        }
        
        return isolated;
    }
    
    private static Bitboard GetFileMask(int file)
    {
        var mask = Bitboard.Empty;
        for (var rank = 0; rank < 8; rank++)
        {
            mask |= SquareExtensions.FromFileRank(file, rank).ToBitboard();
        }
        return mask;
    }
    
    private static int EvaluateKingSafety(Position position, Color color)
    {
        var safety = 0;
        var king = position.GetBitboard(color, PieceType.King);
        
        if (king.IsEmpty()) return 0;
        
        var kingSquare = (Square)king.GetLsbIndex();
        var kingRank = kingSquare.Rank();
        var kingFile = kingSquare.File();
        
        var ourPawns = position.GetBitboard(color, PieceType.Pawn);
        var pawnShield = 0;
        
        if (color == Color.White)
        {
            if (kingRank == 0)
            {
                for (var f = Math.Max(0, kingFile - 1); f <= Math.Min(7, kingFile + 1); f++)
                {
                    if ((ourPawns & SquareExtensions.FromFileRank(f, 1).ToBitboard()).IsNotEmpty())
                        pawnShield += 20;
                    if ((ourPawns & SquareExtensions.FromFileRank(f, 2).ToBitboard()).IsNotEmpty())
                        pawnShield += 10;
                }
            }
        }
        else
        {
            if (kingRank == 7)
            {
                for (var f = Math.Max(0, kingFile - 1); f <= Math.Min(7, kingFile + 1); f++)
                {
                    if ((ourPawns & SquareExtensions.FromFileRank(f, 6).ToBitboard()).IsNotEmpty())
                        pawnShield += 20;
                    if ((ourPawns & SquareExtensions.FromFileRank(f, 5).ToBitboard()).IsNotEmpty())
                        pawnShield += 10;
                }
            }
        }
        
        safety += pawnShield;
        
        return safety;
    }
    
    private static int EvaluateMobility(Position position, Color color)
    {
        var mobility = 0;
        var occupied = position.OccupiedSquares();
        var ourPieces = position.GetBitboard(color);
        
        var knights = position.GetBitboard(color, PieceType.Knight);
        while (knights.IsNotEmpty())
        {
            knights = knights.PopLsb(out var sq);
            var square = (Square)sq;
            var attacks = GetKnightAttacks(square) & ~ourPieces;
            mobility += Bitboard.PopCount(attacks) * 4;
        }
        
        var bishops = position.GetBitboard(color, PieceType.Bishop);
        while (bishops.IsNotEmpty())
        {
            bishops = bishops.PopLsb(out var sq);
            var square = (Square)sq;
            var attacks = GetBishopAttacks(square, occupied) & ~ourPieces;
            mobility += Bitboard.PopCount(attacks) * 3;
        }
        
        var rooks = position.GetBitboard(color, PieceType.Rook);
        while (rooks.IsNotEmpty())
        {
            rooks = rooks.PopLsb(out var sq);
            var square = (Square)sq;
            var attacks = GetRookAttacks(square, occupied) & ~ourPieces;
            mobility += Bitboard.PopCount(attacks) * 2;
        }
        
        var queens = position.GetBitboard(color, PieceType.Queen);
        while (queens.IsNotEmpty())
        {
            queens = queens.PopLsb(out var sq);
            var square = (Square)sq;
            var attacks = GetQueenAttacks(square, occupied) & ~ourPieces;
            mobility += Bitboard.PopCount(attacks);
        }
        
        return mobility;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetKnightAttacks(Square square)
    {
        return AttackTables.KnightAttacks(square);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetBishopAttacks(Square square, Bitboard occupied)
    {
        return MoveGeneration.MagicBitboards.GetBishopAttacks(square, occupied);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetRookAttacks(Square square, Bitboard occupied)
    {
        return MoveGeneration.MagicBitboards.GetRookAttacks(square, occupied);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bitboard GetQueenAttacks(Square square, Bitboard occupied)
    {
        return GetBishopAttacks(square, occupied) | GetRookAttacks(square, occupied);
    }
}