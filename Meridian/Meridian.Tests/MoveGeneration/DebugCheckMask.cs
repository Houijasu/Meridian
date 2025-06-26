#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.Board;
using Meridian.Core.MoveGeneration;
using System.Reflection;

namespace Meridian.Tests.MoveGeneration;

[TestClass]
public class DebugCheckMask
{
    private readonly MoveGenerator _moveGenerator = new();

    [TestMethod]
    public void TestCheckMaskCalculation()
    {
        // Position: White to move, Black just played f7-f5
        var fen = "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3";
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess);
        
        var position = positionResult.Value;
        
        // Use reflection to access private fields
        var moveGeneratorType = typeof(MoveGenerator);
        
        // Set the position field
        var positionField = moveGeneratorType.GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(positionField);
        positionField.SetValue(_moveGenerator, position);
        
        // Call CalculateCheckersAndPinned
        var calculateMethod = moveGeneratorType.GetMethod("CalculateCheckersAndPinned", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(calculateMethod);
        calculateMethod.Invoke(_moveGenerator, null);
        
        // Get the check-related fields
        var checkersField = moveGeneratorType.GetField("_checkers", BindingFlags.NonPublic | BindingFlags.Instance);
        var pinnedField = moveGeneratorType.GetField("_pinned", BindingFlags.NonPublic | BindingFlags.Instance);
        var checkMaskField = moveGeneratorType.GetField("_checkMask", BindingFlags.NonPublic | BindingFlags.Instance);
        var inCheckField = moveGeneratorType.GetField("_inCheck", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var checkers = (Bitboard)checkersField!.GetValue(_moveGenerator)!;
        var pinned = (Bitboard)pinnedField!.GetValue(_moveGenerator)!;
        var checkMask = (Bitboard)checkMaskField!.GetValue(_moveGenerator)!;
        var inCheck = (bool)inCheckField!.GetValue(_moveGenerator)!;
        
        Console.WriteLine($"In check: {inCheck}");
        Console.WriteLine($"Checkers: {GetSquareList(checkers)}");
        Console.WriteLine($"Pinned pieces: {GetSquareList(pinned)}");
        Console.WriteLine($"Check mask: {GetSquareList(checkMask)}");
        Console.WriteLine($"Check mask value: 0x{checkMask.Value:X16}");
        
        // If not in check, check mask should be all 1s (Bitboard.Full)
        if (!inCheck)
        {
            Assert.AreEqual(Bitboard.Full.Value, checkMask.Value, "Check mask should be full when not in check");
        }
        
        // Let's check specific squares
        Console.WriteLine($"\nChecking if specific squares are in check mask:");
        Console.WriteLine($"d2 in check mask: {(checkMask & Square.D2.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"e3 in check mask: {(checkMask & Square.E3.ToBitboard()).IsNotEmpty()}");
        Console.WriteLine($"d6 in check mask: {(checkMask & Square.D6.ToBitboard()).IsNotEmpty()}");
        
        // Test bishop moves specifically
        var c1 = Square.C1;
        var bishopAttacks = MagicBitboards.GetBishopAttacks(c1, position.OccupiedSquares());
        var whitePieces = position.GetBitboard(Color.White);
        var targets = ~whitePieces & checkMask;
        var validBishopMoves = bishopAttacks & targets;
        
        Console.WriteLine($"\nBishop on c1:");
        Console.WriteLine($"Raw attacks: {GetSquareList(bishopAttacks)}");
        Console.WriteLine($"Valid targets (after check mask): {GetSquareList(validBishopMoves)}");
    }
    
    private string GetSquareList(Bitboard bb)
    {
        var squares = new List<string>();
        var temp = bb;
        while (temp.IsNotEmpty())
        {
            var sq = (Square)temp.GetLsbIndex();
            squares.Add(sq.ToAlgebraic());
            temp = temp.RemoveLsb();
        }
        return string.Join(", ", squares);
    }
}