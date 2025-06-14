using Meridian.Core;

namespace Meridian.Debug;

public static class TraceGeneration
{
    public static void TraceBlackMoveGeneration()
    {
        // After d5d6 in Kiwipete
        var board = FenParser.Parse("r3k2r/p1ppqpb1/bn1Ppnp1/4N3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R b KQkq - 0 1");
        
        Console.WriteLine("Tracing black move generation:");
        Console.WriteLine($"Side to move: {board.SideToMove}");
        Console.WriteLine();
        
        // Generate moves with custom implementation to debug
        MoveList moves = new();
        
        Console.WriteLine("Generating pawn moves...");
        GeneratePawnMovesDebug(ref board, ref moves, Color.Black);
        int pawnsEnd = moves.Count;
        Console.WriteLine($"  Generated {pawnsEnd} pawn moves");
        
        Console.WriteLine("Generating knight moves...");
        GenerateKnightMovesDebug(ref board, ref moves, board.BlackKnights);
        Console.WriteLine($"  Generated {moves.Count - pawnsEnd} knight moves");
        int knightsEnd = moves.Count;
        
        Console.WriteLine("Generating bishop moves...");
        GenerateBishopMovesDebug(ref board, ref moves, board.BlackBishops);
        Console.WriteLine($"  Generated {moves.Count - knightsEnd} bishop moves");
        int bishopsEnd = moves.Count;
        
        Console.WriteLine("Generating rook moves...");
        GenerateRookMovesDebug(ref board, ref moves, board.BlackRooks);
        Console.WriteLine($"  Generated {moves.Count - bishopsEnd} rook moves");
        int rooksEnd = moves.Count;
        
        Console.WriteLine("Generating queen moves...");
        GenerateQueenMovesDebug(ref board, ref moves, board.BlackQueens);
        Console.WriteLine($"  Generated {moves.Count - rooksEnd} queen moves");
        int queensEnd = moves.Count;
        
        Console.WriteLine("Generating king moves...");
        GenerateKingMovesDebug(ref board, ref moves, Color.Black);
        Console.WriteLine($"  Generated {moves.Count - queensEnd} king moves");
        
        Console.WriteLine($"\nTotal moves generated: {moves.Count}");
        
        // Count castling moves and print last few moves
        Console.WriteLine("\nLast 10 moves in list:");
        for (int i = Math.Max(0, moves.Count - 10); i < moves.Count; i++)
        {
            Console.WriteLine($"  [{i}] {moves[i]} (Type: {moves[i].Type})");
        }
        
        int castling = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].Type == MoveType.Castle)
            {
                castling++;
                Console.WriteLine($"Castling move found at index {i}: {moves[i]}");
            }
        }
        Console.WriteLine($"Total castling moves: {castling}");
    }
    
    private static void GeneratePawnMovesDebug(ref BoardState board, ref MoveList moves, Color color)
    {
        // Actually generate pawn moves properly
        ulong pawns = board.BlackPawns;
        ulong enemies = board.WhitePieces;
        ulong empty = board.EmptySquares;

        // Single pushes
        ulong singlePushes = Bitboard.ShiftSouth(pawns) & empty;
        while (singlePushes != 0)
        {
            int to = Bitboard.PopLsb(ref singlePushes);
            int from = to + 8;
            
            if (to <= 7) // Promotion
            {
                moves.Add(new Move((Square)from, (Square)to, MoveType.Normal, Piece.Queen));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Normal, Piece.Rook));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Normal, Piece.Bishop));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Normal, Piece.Knight));
            }
            else
            {
                moves.Add(new Move((Square)from, (Square)to));
            }
        }

        // Double pushes
        ulong rank7Pawns = pawns & Bitboard.Rank7;
        ulong rank6Empty = Bitboard.ShiftSouth(rank7Pawns) & empty;
        ulong doublePushes = Bitboard.ShiftSouth(rank6Empty) & empty;
        while (doublePushes != 0)
        {
            int to = Bitboard.PopLsb(ref doublePushes);
            int from = to + 16;
            moves.Add(new Move((Square)from, (Square)to));
        }

        // Captures
        ulong capturesEast = Bitboard.ShiftSouthEast(pawns) & enemies;
        while (capturesEast != 0)
        {
            int to = Bitboard.PopLsb(ref capturesEast);
            int from = to + 7;
            
            if (to <= 7) // Promotion capture
            {
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Queen));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Rook));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Bishop));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Knight));
            }
            else
            {
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture));
            }
        }

        ulong capturesWest = Bitboard.ShiftSouthWest(pawns) & enemies;
        while (capturesWest != 0)
        {
            int to = Bitboard.PopLsb(ref capturesWest);
            int from = to + 9;
            
            if (to <= 7) // Promotion capture
            {
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Queen));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Rook));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Bishop));
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture, Piece.Knight));
            }
            else
            {
                moves.Add(new Move((Square)from, (Square)to, MoveType.Capture));
            }
        }

        // En passant - none in this position
    }
    
    private static void GenerateKnightMovesDebug(ref BoardState board, ref MoveList moves, ulong knights)
    {
        // Simplified
        ulong friendly = board.BlackPieces;
        while (knights != 0)
        {
            int from = Bitboard.PopLsb(ref knights);
            ulong attacks = Attacks.GetKnightAttacks((Square)from) & ~friendly;
            while (attacks != 0)
            {
                int to = Bitboard.PopLsb(ref attacks);
                bool isCapture = Bitboard.GetBit(board.AllPieces, to);
                moves.Add(new Move((Square)from, (Square)to, isCapture ? MoveType.Capture : MoveType.Normal));
            }
        }
    }
    
    private static void GenerateBishopMovesDebug(ref BoardState board, ref MoveList moves, ulong bishops)
    {
        ulong friendly = board.BlackPieces;
        ulong occupied = board.AllPieces;
        while (bishops != 0)
        {
            int from = Bitboard.PopLsb(ref bishops);
            ulong attacks = Attacks.GetBishopAttacks((Square)from, occupied) & ~friendly;
            while (attacks != 0)
            {
                int to = Bitboard.PopLsb(ref attacks);
                bool isCapture = Bitboard.GetBit(board.AllPieces, to);
                moves.Add(new Move((Square)from, (Square)to, isCapture ? MoveType.Capture : MoveType.Normal));
            }
        }
    }
    
    private static void GenerateRookMovesDebug(ref BoardState board, ref MoveList moves, ulong rooks)
    {
        ulong friendly = board.BlackPieces;
        ulong occupied = board.AllPieces;
        while (rooks != 0)
        {
            int from = Bitboard.PopLsb(ref rooks);
            ulong attacks = Attacks.GetRookAttacks((Square)from, occupied) & ~friendly;
            while (attacks != 0)
            {
                int to = Bitboard.PopLsb(ref attacks);
                bool isCapture = Bitboard.GetBit(board.AllPieces, to);
                moves.Add(new Move((Square)from, (Square)to, isCapture ? MoveType.Capture : MoveType.Normal));
            }
        }
    }
    
    private static void GenerateQueenMovesDebug(ref BoardState board, ref MoveList moves, ulong queens)
    {
        ulong friendly = board.BlackPieces;
        ulong occupied = board.AllPieces;
        while (queens != 0)
        {
            int from = Bitboard.PopLsb(ref queens);
            ulong attacks = Attacks.GetQueenAttacks((Square)from, occupied) & ~friendly;
            while (attacks != 0)
            {
                int to = Bitboard.PopLsb(ref attacks);
                bool isCapture = Bitboard.GetBit(board.AllPieces, to);
                moves.Add(new Move((Square)from, (Square)to, isCapture ? MoveType.Capture : MoveType.Normal));
            }
        }
    }
    
    private static void GenerateKingMovesDebug(ref BoardState board, ref MoveList moves, Color color)
    {
        Console.WriteLine("  Inside GenerateKingMovesDebug for Black");
        
        ulong king = board.BlackKing;
        ulong friendly = board.BlackPieces;
        
        int from = Bitboard.BitScanForward(king);
        Console.WriteLine($"  King at: {(Square)from}");
        
        ulong attacks = Attacks.GetKingAttacks((Square)from) & ~friendly;
        int normalMoves = 0;
        while (attacks != 0)
        {
            int to = Bitboard.PopLsb(ref attacks);
            bool isCapture = Bitboard.GetBit(board.AllPieces, to);
            moves.Add(new Move((Square)from, (Square)to, isCapture ? MoveType.Capture : MoveType.Normal));
            normalMoves++;
        }
        Console.WriteLine($"  Normal king moves: {normalMoves}");
        
        // Castling for black
        Console.WriteLine($"  Checking black castling...");
        Console.WriteLine($"  Castling rights has BlackKingSide: {(board.CastlingRights & CastlingRights.BlackKingSide) != 0}");
        
        if ((board.CastlingRights & CastlingRights.BlackKingSide) != 0)
        {
            Console.WriteLine("    Has kingside rights");
            bool squaresEmpty = (board.AllPieces & 0x6000000000000000UL) == 0;
            Console.WriteLine($"    Squares empty: {squaresEmpty}");
            if (squaresEmpty)
            {
                bool e8Safe = !Attacks.IsSquareAttacked(ref board, Square.E8, Color.White);
                bool f8Safe = !Attacks.IsSquareAttacked(ref board, Square.F8, Color.White);
                bool g8Safe = !Attacks.IsSquareAttacked(ref board, Square.G8, Color.White);
                Console.WriteLine($"    e8 safe: {e8Safe}, f8 safe: {f8Safe}, g8 safe: {g8Safe}");
                
                if (e8Safe && f8Safe && g8Safe)
                {
                    Console.WriteLine("    Adding kingside castle!");
                    moves.Add(new Move(Square.E8, Square.G8, MoveType.Castle));
                }
            }
        }
        
        if ((board.CastlingRights & CastlingRights.BlackQueenSide) != 0)
        {
            Console.WriteLine("    Has queenside rights");
            bool squaresEmpty = (board.AllPieces & 0x0E00000000000000UL) == 0;
            Console.WriteLine($"    Squares empty: {squaresEmpty}");
            if (squaresEmpty)
            {
                bool e8Safe = !Attacks.IsSquareAttacked(ref board, Square.E8, Color.White);
                bool d8Safe = !Attacks.IsSquareAttacked(ref board, Square.D8, Color.White);
                bool c8Safe = !Attacks.IsSquareAttacked(ref board, Square.C8, Color.White);
                Console.WriteLine($"    e8 safe: {e8Safe}, d8 safe: {d8Safe}, c8 safe: {c8Safe}");
                
                if (e8Safe && d8Safe && c8Safe)
                {
                    Console.WriteLine("    Adding queenside castle!");
                    moves.Add(new Move(Square.E8, Square.C8, MoveType.Castle));
                }
            }
        }
    }
}