using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meridian.Core.NNUE;
using Meridian.Core.Board;
using System;
using System.IO;

namespace Meridian.Tests.NNUE;

[TestClass]
public class NNUENetworkTests
{
    private NNUENetwork _network;
    private Position _startPosition;

    [TestInitialize]
    public void Setup()
    {
        _network = new NNUENetwork();
        _startPosition = new Position();
    }

    [TestMethod]
    public void TestNetworkInitialization()
    {
        // Test that network initializes correctly
        Assert.IsNotNull(_network);
        Assert.IsFalse(_network.IsLoaded);
    }

    [TestMethod]
    public void TestAccumulatorInitialization()
    {
        // Test that accumulator can be initialized without network loaded
        _network.InitializeAccumulator(_startPosition);

        // Should not throw exception even if network is not loaded
        Assert.IsFalse(_network.IsLoaded);
    }

    [TestMethod]
    public void TestEvaluateWithoutNetworkLoaded()
    {
        // Test evaluation without network loaded should return 0
        var evaluation = _network.Evaluate(_startPosition);
        Assert.AreEqual(0, evaluation);
    }

    [TestMethod]
    public void TestLoadNonExistentNetwork()
    {
        // Test loading a non-existent network file
        var result = _network.LoadNetwork("non_existent_file.nnue");
        Assert.IsFalse(result);
        Assert.IsFalse(_network.IsLoaded);
    }

    [TestMethod]
    public void TestNNUEConstants()
    {
        // Test that constants are reasonable
        Assert.AreEqual(768, NNUEConstants.InputDimensions);
        Assert.AreEqual(1024, NNUEConstants.L1Size);
        Assert.AreEqual(8, NNUEConstants.L2Size);
        Assert.AreEqual(32, NNUEConstants.L3Size);
        Assert.AreEqual(1, NNUEConstants.OutputDimensions);
        Assert.AreEqual(6, NNUEConstants.PieceTypes);
        Assert.AreEqual(2, NNUEConstants.Colors);
        Assert.AreEqual(10, NNUEConstants.KingBuckets);
    }

    [TestMethod]
    public void TestFeatureIndexing()
    {
        // Test piece type indexing
        Assert.AreEqual(0, NNUEConstants.GetPieceTypeIndex(PieceType.Pawn));
        Assert.AreEqual(1, NNUEConstants.GetPieceTypeIndex(PieceType.Knight));
        Assert.AreEqual(2, NNUEConstants.GetPieceTypeIndex(PieceType.Bishop));
        Assert.AreEqual(3, NNUEConstants.GetPieceTypeIndex(PieceType.Rook));
        Assert.AreEqual(4, NNUEConstants.GetPieceTypeIndex(PieceType.Queen));
        Assert.AreEqual(5, NNUEConstants.GetPieceTypeIndex(PieceType.King));
    }

    [TestMethod]
    public void TestFeatureWeightIndexing()
    {
        // Test feature weight indexing
        int pieceType = 0; // Pawn
        int square = 8; // a2
        int kingSquare = 4; // e1

        int whiteIndexWhitePiece = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, kingSquare, true, false);
        int blackIndexWhitePiece = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, kingSquare, true, true);
        int whiteIndexBlackPiece = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, kingSquare, false, false);
        int blackIndexBlackPiece = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, kingSquare, false, true);

        Assert.IsTrue(whiteIndexWhitePiece >= 0);
        Assert.IsTrue(blackIndexWhitePiece >= 0);
        Assert.IsTrue(whiteIndexBlackPiece >= 0);
        Assert.IsTrue(blackIndexBlackPiece >= 0);
        Assert.AreNotEqual(whiteIndexWhitePiece, blackIndexWhitePiece); // Should be different due to perspective
        Assert.AreNotEqual(whiteIndexWhitePiece, whiteIndexBlackPiece); // Should be different due to piece color
    }

    [TestMethod]
    public void TestKingBucketing()
    {
        // Test king bucket assignment for 10-bucket system
        Assert.AreEqual(0, NNUEConstants.GetKingBucket(0));  // a1
        Assert.AreEqual(2, NNUEConstants.GetKingBucket(7));  // h1
        Assert.AreEqual(8, NNUEConstants.GetKingBucket(32)); // a5
        Assert.AreEqual(9, NNUEConstants.GetKingBucket(39)); // h5
    }

    [TestMethod]
    public void TestClippedReLU()
    {
        // Test clipped ReLU activation
        Assert.AreEqual(0, NNUEConstants.ClippedReLU(-100));
        Assert.AreEqual(0, NNUEConstants.ClippedReLU(0));
        Assert.AreEqual(50, NNUEConstants.ClippedReLU(50));
        Assert.AreEqual(127, NNUEConstants.ClippedReLU(127));
        Assert.AreEqual(127, NNUEConstants.ClippedReLU(200));
    }

    [TestMethod]
    public void TestExpectedFileSize()
    {
        // Test that expected file size calculation is reasonable
        var expectedSize = NNUEConstants.ExpectedFileSize;
        Assert.IsTrue(expectedSize > 0);
        Assert.IsTrue(expectedSize < 1000000000); // Should be less than 1GB
    }

    [TestMethod]
    public void TestAccumulatorOperations()
    {
        var accumulator = new Accumulator();

        // Test initialization
        accumulator.Reset();
        Assert.IsFalse(accumulator.IsComputed(0));
        Assert.IsFalse(accumulator.IsComputed(1));

        // Test getting accumulation arrays
        var whiteAccum = accumulator.GetAccumulation(0);
        var blackAccum = accumulator.GetAccumulation(1);

        Assert.IsNotNull(whiteAccum);
        Assert.IsNotNull(blackAccum);
        Assert.AreEqual(NNUEConstants.L1Size, whiteAccum.Length);
        Assert.AreEqual(NNUEConstants.L1Size, blackAccum.Length);
    }

    [TestMethod]
    public void TestAccumulatorCopy()
    {
        var accumulator1 = new Accumulator();
        var accumulator2 = new Accumulator();

        // Modify accumulator1
        accumulator1.SetComputed(0, true);
        accumulator1.GetAccumulation(0)[0] = 100;

        // Copy to accumulator2
        accumulator2.CopyFrom(accumulator1);

        // Verify copy
        Assert.AreEqual(accumulator1.IsComputed(0), accumulator2.IsComputed(0));
        Assert.AreEqual(accumulator1.GetAccumulation(0)[0], accumulator2.GetAccumulation(0)[0]);
    }

    [TestMethod]
    public void TestAccumulatorIntegrity()
    {
        var accumulator = new Accumulator();

        // Should not throw exception for valid accumulator
        accumulator.ValidateIntegrity();

        // Test diagnostic methods
        var sum = accumulator.GetAccumulationSum(0);
        Assert.AreEqual(0, sum); // Should be zero after reset
    }

    [TestMethod]
    public void TestUpdateAccumulatorWithoutNetwork()
    {
        // Test that accumulator update doesn't crash without network
        var move = new Move(Square.e2, Square.e4, MoveType.Normal);
        _network.UpdateAccumulator(_startPosition, move);

        // Should not throw exception
        Assert.IsFalse(_network.IsLoaded);
    }

    [TestMethod]
    public void TestCreateMockNNUEFile()
    {
        // Create a mock NNUE file for testing
        var mockFile = Path.GetTempFileName();

        try
        {
            using (var writer = new BinaryWriter(File.OpenWrite(mockFile)))
            {
                // Write header
                var header = new byte[NNUEConstants.ObsidianHeaderSize];
                writer.Write(header);

                // Write minimal data to avoid crashes
                for (int i = 0; i < 1000; i++)
                {
                    writer.Write((short)0);
                }
            }

            // Test loading - should not crash but may not succeed
            var result = _network.LoadNetwork(mockFile);

            // Either succeeds or fails gracefully
            Assert.IsTrue(result || !result); // Just test it doesn't crash
        }
        finally
        {
            if (File.Exists(mockFile))
            {
                File.Delete(mockFile);
            }
        }
    }

    [TestMethod]
    public void TestNullArgumentHandling()
    {
        // Test null argument handling
        Assert.ThrowsException<ArgumentNullException>(() => _network.InitializeAccumulator(null));
        Assert.ThrowsException<ArgumentNullException>(() => _network.UpdateAccumulator(null, new Move()));
        Assert.ThrowsException<ArgumentNullException>(() => _network.Evaluate(null));
    }

    [TestMethod]
    [TestCategory("Performance")]
    public void TestEvaluationPerformance()
    {
        // Test that evaluation doesn't take too long
        var startTime = DateTime.Now;

        for (int i = 0; i < 1000; i++)
        {
            _network.Evaluate(_startPosition);
        }

        var elapsed = DateTime.Now - startTime;
        Assert.IsTrue(elapsed.TotalMilliseconds < 1000); // Should be very fast without network
    }

    [TestMethod]
    public void TestPositionIndependence()
    {
        // Test that evaluation is consistent for same position
        var eval1 = _network.Evaluate(_startPosition);
        var eval2 = _network.Evaluate(_startPosition);

        Assert.AreEqual(eval1, eval2);
    }
}
