using Meridian.Core;

namespace Meridian.Debug;

public static class DebugFenParsing
{
    public static void Analyze()
    {
        string fen = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        var board = FenParser.Parse(fen);
        
        Console.WriteLine($"Original FEN: {fen}");
        Console.WriteLine($"Parsed FEN:   {FenParser.ToFen(ref board)}");
        Console.WriteLine();
        
        // Check rank 8
        Console.WriteLine("Rank 8 analysis:");
        for (int file = 0; file < 8; file++)
        {
            Square sq = (Square)(56 + file); // Rank 8
            var (piece, color) = board.GetPieceAt(sq);
            Console.WriteLine($"  {sq}: {piece} ({color})");
        }
        
        Console.WriteLine("\nRank 7 analysis:");
        for (int file = 0; file < 8; file++)
        {
            Square sq = (Square)(48 + file); // Rank 7
            var (piece, color) = board.GetPieceAt(sq);
            Console.WriteLine($"  {sq}: {piece} ({color})");
        }
        
        // The FEN string "r3k2r/Pppp1ppp/..." means:
        // Rank 8: r...k..r (black rook, empty, empty, empty, black king, empty, empty, black rook)
        // Rank 7: Pppp1ppp (white Pawn, black pawn, black pawn, black pawn, empty, black pawn, black pawn, black pawn)
        
        Console.WriteLine("\nExpected vs Actual:");
        Console.WriteLine("A8: Expected Black Rook, Got " + board.GetPieceAt(Square.A8));
        Console.WriteLine("A7: Expected White Pawn, Got " + board.GetPieceAt(Square.A7));
        
        // Check the white pawn moves
        Console.WriteLine("\nGenerating moves for white:");
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        Console.WriteLine($"Total moves: {moves.Count}");
        Console.WriteLine("\nA7 pawn moves:");
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == Square.A7)
            {
                Console.WriteLine($"  {moves[i]}");
            }
        }
    }
}