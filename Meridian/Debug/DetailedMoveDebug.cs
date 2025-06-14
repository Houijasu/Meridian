using Meridian.Core;

namespace Meridian.Debug;

public static class DetailedMoveDebug
{
    public static void DebugSpecificMove()
    {
        // Kiwipete position
        var board = FenParser.Parse("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        
        Console.WriteLine("Debugging d5d6 in Kiwipete (expected 47, got 41):");
        
        // Make d5d6
        var move = new Move(Square.D5, Square.D6, MoveType.Normal);
        board.MakeMove(move);
        
        Console.WriteLine($"After d5d6:");
        Console.WriteLine($"FEN: {FenParser.ToFen(ref board)}");
        Console.WriteLine($"Castling rights: {board.CastlingRights}");
        Console.WriteLine();
        
        // Generate all black responses
        MoveList responses = new();
        MoveGenerator.GenerateAllMoves(ref board, ref responses);
        
        Console.WriteLine($"Total responses generated: {responses.Count}");
        
        // Count legal responses by type
        var moveTypes = new Dictionary<string, int>();
        int legalCount = 0;
        
        for (int i = 0; i < responses.Count; i++)
        {
            BoardState copy = board;
            board.MakeMove(responses[i]);
            
            if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
            {
                legalCount++;
                
                string type = GetMoveType(ref copy, responses[i]);
                moveTypes[type] = moveTypes.GetValueOrDefault(type) + 1;
                
                // Show castling moves
                if (responses[i].Type == MoveType.Castle)
                {
                    Console.WriteLine($"  Castling: {responses[i]}");
                }
            }
            
            board = copy;
        }
        
        Console.WriteLine($"\nLegal responses: {legalCount}");
        Console.WriteLine("By type:");
        foreach (var kvp in moveTypes)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        
        // Check if black can still castle
        Console.WriteLine($"\nBlack castling rights after d5d6: {board.CastlingRights & (CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide)}");
        
        // Manually check castling conditions for black
        CheckBlackCastling(ref board);
    }
    
    private static void CheckBlackCastling(ref BoardState board)
    {
        Console.WriteLine("\nManual check of black castling conditions:");
        
        // Kingside
        Console.WriteLine("Kingside (O-O):");
        Console.WriteLine($"  Rights: {(board.CastlingRights & CastlingRights.BlackKingSide) != 0}");
        Console.WriteLine($"  Squares empty (f8,g8): {(board.AllPieces & 0x6000000000000000UL) == 0}");
        Console.WriteLine($"  e8 not attacked: {!Attacks.IsSquareAttacked(ref board, Square.E8, Color.White)}");
        Console.WriteLine($"  f8 not attacked: {!Attacks.IsSquareAttacked(ref board, Square.F8, Color.White)}");
        Console.WriteLine($"  g8 not attacked: {!Attacks.IsSquareAttacked(ref board, Square.G8, Color.White)}");
        
        // Queenside
        Console.WriteLine("Queenside (O-O-O):");
        Console.WriteLine($"  Rights: {(board.CastlingRights & CastlingRights.BlackQueenSide) != 0}");
        Console.WriteLine($"  Squares empty (b8,c8,d8): {(board.AllPieces & 0x0E00000000000000UL) == 0}");
        Console.WriteLine($"  e8 not attacked: {!Attacks.IsSquareAttacked(ref board, Square.E8, Color.White)}");
        Console.WriteLine($"  d8 not attacked: {!Attacks.IsSquareAttacked(ref board, Square.D8, Color.White)}");
        Console.WriteLine($"  c8 not attacked: {!Attacks.IsSquareAttacked(ref board, Square.C8, Color.White)}");
    }
    
    private static string GetMoveType(ref BoardState board, Move move)
    {
        if (move.Type == MoveType.Castle) return "Castle";
        if (move.Type == MoveType.EnPassant) return "EnPassant";
        
        var (piece, _) = board.GetPieceAt(move.From);
        string result = piece.ToString();
        
        if (move.IsCapture()) result += " capture";
        if (move.IsPromotion()) result += " promotion";
        
        return result;
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}