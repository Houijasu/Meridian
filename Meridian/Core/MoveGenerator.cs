namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class MoveGenerator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GenerateAllMoves(ref BoardState board, ref MoveList moves)
    {
        moves.Clear();
        
        if (board.SideToMove == Color.White)
        {
            GeneratePawnMoves(ref board, ref moves, Color.White);
            GenerateKnightMoves(ref board, ref moves, board.WhiteKnights);
            GenerateBishopMoves(ref board, ref moves, board.WhiteBishops);
            GenerateRookMoves(ref board, ref moves, board.WhiteRooks);
            GenerateQueenMoves(ref board, ref moves, board.WhiteQueens);
            GenerateKingMoves(ref board, ref moves, Color.White);
        }
        else
        {
            GeneratePawnMoves(ref board, ref moves, Color.Black);
            GenerateKnightMoves(ref board, ref moves, board.BlackKnights);
            GenerateBishopMoves(ref board, ref moves, board.BlackBishops);
            GenerateRookMoves(ref board, ref moves, board.BlackRooks);
            GenerateQueenMoves(ref board, ref moves, board.BlackQueens);
            GenerateKingMoves(ref board, ref moves, Color.Black);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GeneratePawnMoves(ref BoardState board, ref MoveList moves, Color color)
    {
        ulong pawns = color == Color.White ? board.WhitePawns : board.BlackPawns;
        ulong enemies = color == Color.White ? board.BlackPieces : board.WhitePieces;
        ulong empty = board.EmptySquares;

        if (color == Color.White)
        {
            // Single pushes
            ulong singlePushes = Bitboard.ShiftNorth(pawns) & empty;
            while (singlePushes != 0)
            {
                int to = Bitboard.PopLsb(ref singlePushes);
                int from = to - 8;
                
                if (to >= 56) // Promotion
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
            ulong rank2Pawns = pawns & Bitboard.Rank2;
            ulong rank3Empty = Bitboard.ShiftNorth(rank2Pawns) & empty;
            ulong doublePushes = Bitboard.ShiftNorth(rank3Empty) & empty;
            while (doublePushes != 0)
            {
                int to = Bitboard.PopLsb(ref doublePushes);
                int from = to - 16;
                moves.Add(new Move((Square)from, (Square)to));
            }

            // Captures
            ulong capturesEast = Bitboard.ShiftNorthEast(pawns) & enemies;
            while (capturesEast != 0)
            {
                int to = Bitboard.PopLsb(ref capturesEast);
                int from = to - 9;
                
                if (to >= 56) // Promotion capture
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

            ulong capturesWest = Bitboard.ShiftNorthWest(pawns) & enemies;
            while (capturesWest != 0)
            {
                int to = Bitboard.PopLsb(ref capturesWest);
                int from = to - 7;
                
                if (to >= 56) // Promotion capture
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

            // En passant
            if (board.EnPassantSquare != Square.None)
            {
                ulong epSquare = board.EnPassantSquare.ToBitboard();
                ulong epCaptures = Attacks.GetPawnAttacks(board.EnPassantSquare, Color.Black) & pawns;
                while (epCaptures != 0)
                {
                    int from = Bitboard.PopLsb(ref epCaptures);
                    moves.Add(new Move((Square)from, board.EnPassantSquare, MoveType.EnPassant));
                }
            }
        }
        else // Black
        {
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

            // En passant
            if (board.EnPassantSquare != Square.None)
            {
                ulong epSquare = board.EnPassantSquare.ToBitboard();
                ulong epCaptures = Attacks.GetPawnAttacks(board.EnPassantSquare, Color.White) & pawns;
                while (epCaptures != 0)
                {
                    int from = Bitboard.PopLsb(ref epCaptures);
                    moves.Add(new Move((Square)from, board.EnPassantSquare, MoveType.EnPassant));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateKnightMoves(ref BoardState board, ref MoveList moves, ulong knights)
    {
        ulong friendly = board.SideToMove == Color.White ? board.WhitePieces : board.BlackPieces;
        
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateBishopMoves(ref BoardState board, ref MoveList moves, ulong bishops)
    {
        ulong friendly = board.SideToMove == Color.White ? board.WhitePieces : board.BlackPieces;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateRookMoves(ref BoardState board, ref MoveList moves, ulong rooks)
    {
        ulong friendly = board.SideToMove == Color.White ? board.WhitePieces : board.BlackPieces;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateQueenMoves(ref BoardState board, ref MoveList moves, ulong queens)
    {
        ulong friendly = board.SideToMove == Color.White ? board.WhitePieces : board.BlackPieces;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateKingMoves(ref BoardState board, ref MoveList moves, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        ulong friendly = color == Color.White ? board.WhitePieces : board.BlackPieces;
        
        int from = Bitboard.BitScanForward(king);
        ulong attacks = Attacks.GetKingAttacks((Square)from) & ~friendly;
        
        while (attacks != 0)
        {
            int to = Bitboard.PopLsb(ref attacks);
            bool isCapture = Bitboard.GetBit(board.AllPieces, to);
            moves.Add(new Move((Square)from, (Square)to, isCapture ? MoveType.Capture : MoveType.Normal));
        }

        // Castling
        if (color == Color.White)
        {
            if ((board.CastlingRights & CastlingRights.WhiteKingSide) != 0 &&
                (board.AllPieces & 0x60UL) == 0 &&
                !Attacks.IsSquareAttacked(ref board, Square.E1, Color.Black) &&
                !Attacks.IsSquareAttacked(ref board, Square.F1, Color.Black) &&
                !Attacks.IsSquareAttacked(ref board, Square.G1, Color.Black))
            {
                moves.Add(new Move(Square.E1, Square.G1, MoveType.Castle));
            }

            if ((board.CastlingRights & CastlingRights.WhiteQueenSide) != 0 &&
                (board.AllPieces & 0x0EUL) == 0 &&
                !Attacks.IsSquareAttacked(ref board, Square.E1, Color.Black) &&
                !Attacks.IsSquareAttacked(ref board, Square.D1, Color.Black) &&
                !Attacks.IsSquareAttacked(ref board, Square.C1, Color.Black))
            {
                moves.Add(new Move(Square.E1, Square.C1, MoveType.Castle));
            }
        }
        else
        {
            if ((board.CastlingRights & CastlingRights.BlackKingSide) != 0 &&
                (board.AllPieces & 0x6000000000000000UL) == 0 &&
                !Attacks.IsSquareAttacked(ref board, Square.E8, Color.White) &&
                !Attacks.IsSquareAttacked(ref board, Square.F8, Color.White) &&
                !Attacks.IsSquareAttacked(ref board, Square.G8, Color.White))
            {
                moves.Add(new Move(Square.E8, Square.G8, MoveType.Castle));
            }

            if ((board.CastlingRights & CastlingRights.BlackQueenSide) != 0 &&
                (board.AllPieces & 0x0E00000000000000UL) == 0 &&
                !Attacks.IsSquareAttacked(ref board, Square.E8, Color.White) &&
                !Attacks.IsSquareAttacked(ref board, Square.D8, Color.White) &&
                !Attacks.IsSquareAttacked(ref board, Square.C8, Color.White))
            {
                moves.Add(new Move(Square.E8, Square.C8, MoveType.Castle));
            }
        }
    }
}