#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System;
using System.Reflection;

namespace Meridian.Tests.Perft;

[TestClass]
public class DebugCheckMask
{
    [TestMethod]
    public void DebugCheckMaskCalculation()
    {
        // Position after f1b5 - Black is in check
        var fenResult = Position.FromFen("rnbqkbnr/ppp1p1pp/8/1B1pPp2/8/8/PPPP1PPP/RNBQK1NR b KQkq - 1 3");
        Assert.IsTrue(fenResult.IsSuccess);
        
        var position = fenResult.Value;
        
        // Create move generator and generate moves to trigger check calculation
        var moveGen = new MoveGenerator();
        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        moveGen.GenerateMoves(position, ref moves);
        
        // Use reflection to access private fields
        var checkMaskField = typeof(MoveGenerator).GetField("_checkMask", BindingFlags.NonPublic | BindingFlags.Instance);
        var checkersField = typeof(MoveGenerator).GetField("_checkers", BindingFlags.NonPublic | BindingFlags.Instance);
        var inCheckField = typeof(MoveGenerator).GetField("_inCheck", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var checkMask = (Bitboard)checkMaskField!.GetValue(moveGen)!;
        var checkers = (Bitboard)checkersField!.GetValue(moveGen)!;
        var inCheck = (bool)inCheckField!.GetValue(moveGen)!;
        
        Console.WriteLine($"In check: {inCheck}");
        Console.WriteLine($"Checkers bitboard: 0x{checkers:X16}");
        Console.WriteLine($"Check mask bitboard: 0x{checkMask:X16}");
        
        // Convert to squares for clarity
        Console.WriteLine("\nChecker squares:");
        var checkersCopy = checkers;
        while (checkersCopy.IsNotEmpty())
        {
            var sq = (Square)checkersCopy.GetLsbIndex();
            Console.WriteLine($"  {sq}");
            checkersCopy = checkersCopy.RemoveLsb();
        }
        
        Console.WriteLine("\nCheck mask squares:");
        var checkMaskCopy = checkMask;
        while (checkMaskCopy.IsNotEmpty())
        {
            var sq = (Square)checkMaskCopy.GetLsbIndex();
            Console.WriteLine($"  {sq}");
            checkMaskCopy = checkMaskCopy.RemoveLsb();
        }
        
        // Check specific squares
        Console.WriteLine("\nChecking if squares are in check mask:");
        Console.WriteLine($"  B5 (bishop): {(checkMask & Square.B5.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"  C6 (block): {(checkMask & Square.C6.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"  D7 (block): {(checkMask & Square.D7.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"  E7 (block): {(checkMask & Square.E7.ToBitboard()).IsNotEmpty()}");
        
        // Now let's check what moves were generated
        Console.WriteLine($"\nMoves generated: {moves.Count}");
        for (int i = 0; i < moves.Count; i++)
        {
            Console.WriteLine($"  {moves[i].ToUci()}");
        }
    }
}