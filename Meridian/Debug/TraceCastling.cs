using Meridian.Core;

namespace Meridian.Debug;

public static class TraceCastling
{
    public static void TraceBlackCastling()
    {
        // After d5d6 in Kiwipete
        var board = FenParser.Parse("r3k2r/p1ppqpb1/bn1Ppnp1/4N3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R b KQkq - 0 1");
        
        Console.WriteLine("Tracing black castling generation after d5d6:");
        Console.WriteLine($"FEN: {FenParser.ToFen(ref board)}");
        Console.WriteLine($"Side to move: {board.SideToMove}");
        Console.WriteLine($"Castling rights: {board.CastlingRights}");
        Console.WriteLine();
        
        // Manually trace through GenerateKingMoves
        ulong king = board.BlackKing;
        Console.WriteLine($"Black king bitboard: 0x{king:X16}");
        Console.WriteLine($"Black king square: {(Square)Bitboard.BitScanForward(king)}");
        
        // Check kingside castling conditions step by step
        Console.WriteLine("\nKingside castling checks:");
        bool hasKingsideRights = (board.CastlingRights & CastlingRights.BlackKingSide) != 0;
        Console.WriteLine($"1. Has rights: {hasKingsideRights}");
        
        ulong kingsideOccupancy = board.AllPieces & 0x6000000000000000UL;
        Console.WriteLine($"2. f8,g8 empty: {kingsideOccupancy == 0} (occupancy: 0x{kingsideOccupancy:X16})");
        
        bool e8Safe = !Attacks.IsSquareAttacked(ref board, Square.E8, Color.White);
        bool f8Safe = !Attacks.IsSquareAttacked(ref board, Square.F8, Color.White);
        bool g8Safe = !Attacks.IsSquareAttacked(ref board, Square.G8, Color.White);
        
        Console.WriteLine($"3. e8 not attacked: {e8Safe}");
        Console.WriteLine($"4. f8 not attacked: {f8Safe}");
        Console.WriteLine($"5. g8 not attacked: {g8Safe}");
        
        bool canCastleKingside = hasKingsideRights && kingsideOccupancy == 0 && e8Safe && f8Safe && g8Safe;
        Console.WriteLine($"Can castle kingside: {canCastleKingside}");
        
        // Similar for queenside
        Console.WriteLine("\nQueenside castling checks:");
        bool hasQueensideRights = (board.CastlingRights & CastlingRights.BlackQueenSide) != 0;
        Console.WriteLine($"1. Has rights: {hasQueensideRights}");
        
        ulong queensideOccupancy = board.AllPieces & 0x0E00000000000000UL;
        Console.WriteLine($"2. b8,c8,d8 empty: {queensideOccupancy == 0} (occupancy: 0x{queensideOccupancy:X16})");
        
        bool d8Safe = !Attacks.IsSquareAttacked(ref board, Square.D8, Color.White);
        bool c8Safe = !Attacks.IsSquareAttacked(ref board, Square.C8, Color.White);
        
        Console.WriteLine($"3. e8 not attacked: {e8Safe}");
        Console.WriteLine($"4. d8 not attacked: {d8Safe}");
        Console.WriteLine($"5. c8 not attacked: {c8Safe}");
        
        bool canCastleQueenside = hasQueensideRights && queensideOccupancy == 0 && e8Safe && d8Safe && c8Safe;
        Console.WriteLine($"Can castle queenside: {canCastleQueenside}");
        
        // Now generate moves and see what we get
        Console.WriteLine("\nGenerating all moves:");
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        int castlingCount = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].Type == MoveType.Castle)
            {
                castlingCount++;
                Console.WriteLine($"Found castling move: {moves[i]}");
            }
        }
        
        Console.WriteLine($"Total castling moves generated: {castlingCount}");
    }
}