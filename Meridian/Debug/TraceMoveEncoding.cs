using Meridian.Core;

namespace Meridian.Debug;

public static class TraceMoveEncoding
{
    public static void TestCastlingEncoding()
    {
        Console.WriteLine("Testing Move encoding for castling:\n");
        
        // Test white kingside castle
        var whiteKingside = new Move(Square.E1, Square.G1, MoveType.Castle);
        AnalyzeMove(whiteKingside, "White O-O");
        
        // Test black kingside castle  
        var blackKingside = new Move(Square.E8, Square.G8, MoveType.Castle);
        AnalyzeMove(blackKingside, "Black O-O");
        
        // Test black queenside castle
        var blackQueenside = new Move(Square.E8, Square.C8, MoveType.Castle);
        AnalyzeMove(blackQueenside, "Black O-O-O");
        
        // Test normal king move for comparison
        var normalKing = new Move(Square.E8, Square.D8, MoveType.Normal);
        AnalyzeMove(normalKing, "Normal king move");
        
        // Test with promotions to see the conflict
        Console.WriteLine("\nTesting promotion encoding:");
        var queenPromo = new Move(Square.E7, Square.E8, MoveType.Normal, Piece.Queen);
        AnalyzeMove(queenPromo, "Queen promotion");
        
        var knightPromo = new Move(Square.E7, Square.E8, MoveType.Normal, Piece.Knight);
        AnalyzeMove(knightPromo, "Knight promotion");
        
        var bishopPromo = new Move(Square.E7, Square.E8, MoveType.Normal, Piece.Bishop);
        AnalyzeMove(bishopPromo, "Bishop promotion");
    }
    
    private static void AnalyzeMove(Move move, string description)
    {
        // Use reflection to get the raw data
        var field = typeof(Move).GetField("_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ushort rawData = (ushort)field.GetValue(move);
        
        Console.WriteLine($"{description}:");
        Console.WriteLine($"  From: {move.From} ({(int)move.From})");
        Console.WriteLine($"  To: {move.To} ({(int)move.To})");
        Console.WriteLine($"  Type: {move.Type} ({(int)move.Type})");
        Console.WriteLine($"  Promotion: {move.PromotionPiece}");
        Console.WriteLine($"  Raw data: 0x{rawData:X4} ({Convert.ToString(rawData, 2).PadLeft(16, '0')})");
        Console.WriteLine($"  Bits 0-5 (from): {rawData & 0x3F}");
        Console.WriteLine($"  Bits 6-11 (to): {(rawData >> 6) & 0x3F}");
        Console.WriteLine($"  Bits 12-13 (type): {(rawData >> 12) & 0x3}");
        Console.WriteLine($"  Bits 14-15 (promo): {(rawData >> 14) & 0x3}");
        Console.WriteLine();
    }
}