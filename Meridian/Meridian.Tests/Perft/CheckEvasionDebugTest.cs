#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;

namespace Meridian.Tests.Perft;

[TestClass]
public class CheckEvasionDebugTest
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void DebugCheckEvasionAfterBf1b5()
    {
        // Position after 1.e4 e6 2.e5 f5 3.Bb5+
        var fen = "rnbqkbnr/ppp1p1pp/8/1B1pPp2/8/8/PPPP1PPP/RNBQK2R b KQkq - 1 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        Console.WriteLine("Position after Bb5+:");
        Console.WriteLine($"FEN: {fen}");
        
        // Check if Black king is in check
        var blackKing = position.GetBitboard(Color.Black, PieceType.King);
        Assert.IsTrue(blackKing.IsNotEmpty());
        var kingSquare = (Square)blackKing.GetLsbIndex();
        var isInCheck = MoveGenerator.IsSquareAttacked(position, kingSquare, Color.White);
        Console.WriteLine($"Black king on {kingSquare}: {(isInCheck ? "IN CHECK" : "not in check")}");
        
        // Generate moves
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        _moveGenerator.GenerateMoves(position, ref moves);
        
        Console.WriteLine($"\nBlack has {moves.Count} legal moves");
        Console.WriteLine("Expected: 6 moves according to Stockfish");
        
        // List all moves
        Console.WriteLine("\nLegal moves:");
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            Console.WriteLine($"  {move.ToUci()} - {position.GetPiece(move.From)} from {move.From} to {move.To}");
        }
        
        // Check what Stockfish says are the legal moves
        Console.WriteLine("\nExpected moves (from Stockfish):");
        Console.WriteLine("  b8c6 - Knight blocks check");
        Console.WriteLine("  c7c6 - Pawn blocks check");
        Console.WriteLine("  d7d6 - Pawn blocks check");
        Console.WriteLine("  f8e7 - Bishop blocks check");
        Console.WriteLine("  d8e7 - Queen blocks check");
        Console.WriteLine("  e8e7 - King moves out of check");
        
        // Analyze why we might be missing moves
        if (moves.Count < 6)
        {
            Console.WriteLine("\nDebugging check evasion:");
            
            // Test specific moves
            var testMoves = new[] {
                ("b8c6", Square.B8, Square.C6),
                ("c7c6", Square.C7, Square.C6),
                ("d7d6", Square.D7, Square.D6),
                ("f8e7", Square.F8, Square.E7),
                ("d8e7", Square.D8, Square.E7),
                ("e8e7", Square.E8, Square.E7)
            };
            
            foreach (var (uci, from, to) in testMoves)
            {
                var piece = position.GetPiece(from);
                Console.WriteLine($"\nTesting {uci}: {piece} from {from} to {to}");
                
                // Check if the move is in our generated list
                var found = false;
                for (int i = 0; i < moves.Count; i++)
                {
                    if (moves[i].From == from && moves[i].To == to)
                    {
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    Console.WriteLine($"  NOT FOUND in generated moves!");
                    
                    // Try to understand why
                    if (piece == Piece.None)
                    {
                        Console.WriteLine($"  No piece at {from}");
                    }
                    else
                    {
                        // Make the move and check if king is still in check
                        var testMove = new Move(from, to, MoveType.None);
                        var undoInfo = position.MakeMove(testMove);
                        
                        var ourKing = position.GetBitboard(Color.Black, PieceType.King);
                        if (ourKing.IsNotEmpty())
                        {
                            var kingSq = (Square)ourKing.GetLsbIndex();
                            var stillInCheck = MoveGenerator.IsSquareAttacked(position, kingSq, Color.White);
                            Console.WriteLine($"  After move, king would be {(stillInCheck ? "STILL IN CHECK" : "safe")}");
                        }
                        
                        position.UnmakeMove(testMove, undoInfo);
                    }
                }
                else
                {
                    Console.WriteLine("  Found in generated moves");
                }
            }
        }
        
        if (moves.Count != 6)
        {
            Assert.Fail($"Expected 6 check evasion moves, got {moves.Count}");
        }
    }
}