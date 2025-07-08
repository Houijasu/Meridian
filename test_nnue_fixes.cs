using System;
using Meridian.Core.Board;
using Meridian.Core.Evaluation;
using Meridian.Core.NNUE;

namespace Meridian.Tests
{
    public class TestNNUEFixes
    {
        public static void RunTests()
        {
            Console.WriteLine("=== Testing NNUE Fixes ===");

            // Test 1: Validate architecture constants
            TestArchitectureConstants();

            // Test 2: Test king bucket scheme
            TestKingBucketScheme();

            // Test 3: Test evaluation on starting position
            TestStartingPositionEvaluation();

            // Test 4: Test phase calculation
            TestPhaseCalculation();

            Console.WriteLine("=== NNUE Fix Tests Complete ===");
        }

        private static void TestArchitectureConstants()
        {
            Console.WriteLine("\n--- Testing Architecture Constants ---");

            // Verify Obsidian architecture
            Console.WriteLine($"King Buckets: {NNUEConstants.KingBuckets} (should be 13)");
            Console.WriteLine($"L1 Size: {NNUEConstants.L1Size} (should be 1536)");
            Console.WriteLine($"L2 Size: {NNUEConstants.L2Size} (should be 16)");
            Console.WriteLine($"L3 Size: {NNUEConstants.L3Size} (should be 32)");
            Console.WriteLine($"NetworkQA: {NNUEConstants.NetworkQA} (should be 255)");
            Console.WriteLine($"NetworkQB: {NNUEConstants.NetworkQB} (should be 128)");

            // Verify expected file size is reasonable for 30.9MB network
            long expectedSize = NNUEConstants.ExpectedFileSize;
            Console.WriteLine($"Expected file size: {expectedSize:N0} bytes");
            Console.WriteLine($"Target range: 25-35 MB for Obsidian network");

            bool sizeOk = expectedSize >= 25_000_000 && expectedSize <= 35_000_000;
            Console.WriteLine($"File size calculation: {(sizeOk ? "PASS" : "FAIL")}");
        }

        private static void TestKingBucketScheme()
        {
            Console.WriteLine("\n--- Testing King Bucket Scheme ---");

            // Test specific squares that should have known bucket values
            var testCases = new (Square square, int expectedBucket)[]
            {
                (Square.A1, 0),  // Corner
                (Square.E1, 3),  // Center files, back rank
                (Square.H1, 0),  // Corner (mirrored)
                (Square.A2, 4),  // Second rank
                (Square.E2, 7),  // Center files, second rank
                (Square.E4, 10), // Middle game squares
                (Square.E8, 3),  // Black back rank (should mirror to white)
            };

            foreach (var (square, expected) in testCases)
            {
                int bucket = NNUEConstants.GetKingBucket((int)square);
                bool correct = bucket == expected;
                Console.WriteLine($"Square {square} -> Bucket {bucket} (expected {expected}): {(correct ? "PASS" : "FAIL")}");
            }
        }

        private static void TestStartingPositionEvaluation()
        {
            Console.WriteLine("\n--- Testing Starting Position Evaluation ---");

            try
            {
                var position = Position.StartingPosition();

                // Test phase calculation
                int phase = NNUEConstants.GetPhase(position);
                Console.WriteLine($"Starting position phase: {phase}");

                // Expected phase: 2*16 + 3*4 + 3*4 + 5*4 + 12*2 = 32 + 12 + 12 + 20 + 24 = 100
                bool phaseOk = phase >= 95 && phase <= 105;
                Console.WriteLine($"Phase calculation: {(phaseOk ? "PASS" : "FAIL")} (expected ~100)");

                // Test if NNUE is available (may not be loaded in test environment)
                if (Evaluator.UseNNUE)
                {
                    int eval = Evaluator.Evaluate(position);
                    Console.WriteLine($"NNUE evaluation: {eval} centipawns");

                    // Starting position should be roughly equal (within ±100 centipawns)
                    bool evalOk = Math.Abs(eval) <= 100;
                    Console.WriteLine($"Evaluation reasonableness: {(evalOk ? "PASS" : "FAIL")} (should be ±100 cp)");
                }
                else
                {
                    Console.WriteLine("NNUE not loaded - cannot test evaluation");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing evaluation: {ex.Message}");
            }
        }

        private static void TestPhaseCalculation()
        {
            Console.WriteLine("\n--- Testing Phase Calculation ---");

            try
            {
                // Test various positions
                var testPositions = new (string fen, string description, int minPhase, int maxPhase)[]
                {
                    ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "Starting position", 95, 105),
                    ("8/8/8/8/8/8/8/K6k w - - 0 1", "King endgame", 0, 5),
                    ("rnbqkbnr/8/8/8/8/8/8/RNBQKBNR w - - 0 1", "No pawns", 65, 75),
                    ("8/pppppppp/8/8/8/8/PPPPPPPP/8 w - - 0 1", "Only pawns", 30, 35),
                };

                foreach (var (fen, description, minPhase, maxPhase) in testPositions)
                {
                    var posResult = Position.FromFen(fen);
                    if (posResult.IsSuccess)
                    {
                        int phase = NNUEConstants.GetPhase(posResult.Value);
                        bool phaseOk = phase >= minPhase && phase <= maxPhase;
                        Console.WriteLine($"{description}: Phase {phase} (expected {minPhase}-{maxPhase}): {(phaseOk ? "PASS" : "FAIL")}");
                    }
                    else
                    {
                        Console.WriteLine($"{description}: Failed to parse FEN");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing phase calculation: {ex.Message}");
            }
        }
    }
}
