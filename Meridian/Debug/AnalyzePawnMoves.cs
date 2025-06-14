using Meridian.Core;

namespace Meridian.Debug;

public static class AnalyzePawnMoves
{
    public static void Analyze()
    {
        var board = FenParser.Parse("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
        
        Console.WriteLine("Analyzing pawn moves in Position 3:");
        Console.WriteLine("Initial: 8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1\n");
        
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        // Focus on pawn moves
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (IsPawnMove(ref board, move))
            {
                BoardState copy = board;
                board.MakeMove(move);
                
                if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
                {
                    Console.WriteLine($"\nAfter {move}:");
                    Console.WriteLine($"FEN: {FenParser.ToFen(ref board)}");
                    
                    // Count responses
                    MoveList responses = new();
                    MoveGenerator.GenerateAllMoves(ref board, ref responses);
                    
                    int legalResponses = 0;
                    Console.WriteLine("Black's responses:");
                    
                    for (int j = 0; j < responses.Count; j++)
                    {
                        BoardState copy2 = board;
                        board.MakeMove(responses[j]);
                        
                        if (!IsKingInCheck(ref board, board.SideToMove.Opposite()))
                        {
                            legalResponses++;
                            Console.WriteLine($"  {responses[j]}");
                        }
                        
                        board = copy2;
                    }
                    
                    Console.WriteLine($"Total legal responses: {legalResponses}");
                }
                
                board = copy;
            }
        }
    }
    
    private static bool IsPawnMove(ref BoardState board, Move move)
    {
        var (piece, _) = board.GetPieceAt(move.From);
        return piece == Piece.Pawn;
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}