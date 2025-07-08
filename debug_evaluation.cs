using System;
using System.IO;
using Meridian.Core.Board;
using Meridian.Core.Evaluation;
using Meridian.Core.MoveGeneration;

namespace Meridian.Debug
{
    public static class DebugEvaluator
    {
        public static void DebugPosition(Position position)
        {
            Console.WriteLine("=== DEBUG EVALUATION ===");
            Console.WriteLine($"Position: {position.ToFen()}");
            Console.WriteLine($"Side to move: {position.SideToMove}");
            Console.WriteLine();

            // Test basic evaluation
            var eval = Evaluator.Evaluate(position);
            Console.WriteLine($"Total evaluation: {eval}");
            Console.WriteLine($"NNUE enabled: {Evaluator.UseNNUE}");
            Console.WriteLine();

            // Test move generation and scoring
            var moveGen = new MoveGenerator();
            var moves = new MoveList();
            moveGen.GenerateMoves(position, ref moves);

            Console.WriteLine($"Generated {moves.Count} moves:");

            for (int i = 0; i < Math.Min(moves.Count, 10); i++)
            {
                var move = moves[i];
                var undoInfo = position.MakeMove(move);
                var moveEval = -Evaluator.Evaluate(position);
                position.UnmakeMove(move, undoInfo);

                Console.WriteLine($"  {i + 1}. {move.ToUci()} -> {moveEval}");
            }

            Console.WriteLine();

            // Test material evaluation
            var whiteMaterial = position.GetMaterial(Color.White);
            var blackMaterial = position.GetMaterial(Color.Black);
            Console.WriteLine($"White material: {whiteMaterial}");
            Console.WriteLine($"Black material: {blackMaterial}");
            Console.WriteLine($"Material difference: {whiteMaterial - blackMaterial}");
            Console.WriteLine();

            // Test piece counts
            Console.WriteLine("Piece counts:");
            foreach (var color in new[] { Color.White, Color.Black })
            {
                Console.WriteLine($"  {color}:");
                foreach (var pieceType in new[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King })
                {
                    var count = Bitboard.PopCount(position.GetBitboard(color, pieceType));
                    Console.WriteLine($"    {pieceType}: {count}");
                }
            }
            Console.WriteLine();

            // Test if evaluation is symmetric
            var mirrorFen = MirrorPosition(position.ToFen());
            var mirrorPos = Position.FromFen(mirrorFen);
            if (mirrorPos.IsOk)
            {
                var mirrorEval = Evaluator.Evaluate(mirrorPos.Value);
                Console.WriteLine($"Mirror evaluation: {mirrorEval}");
                Console.WriteLine($"Evaluation symmetry check: {Math.Abs(eval + mirrorEval)} (should be close to 0)");
            }

            Console.WriteLine("=== END DEBUG ===");
        }

        public static void DebugOpeningMoves()
        {
            Console.WriteLine("=== DEBUGGING OPENING MOVES ===");

            var startPos = Position.StartingPosition();
            DebugPosition(startPos);

            // Test common opening moves
            var commonMoves = new string[] { "e2e4", "d2d4", "g1f3", "c2c4", "a2a3", "h2h3" };

            Console.WriteLine("\nEvaluating common opening moves:");
            foreach (var moveStr in commonMoves)
            {
                if (Move.TryFromUci(moveStr, out var move))
                {
                    var undoInfo = startPos.MakeMove(move);
                    var eval = -Evaluator.Evaluate(startPos);
                    startPos.UnmakeMove(move, undoInfo);

                    Console.WriteLine($"  {moveStr}: {eval}");
                }
            }

            Console.WriteLine("=== END OPENING DEBUG ===");
        }

        private static string MirrorPosition(string fen)
        {
            var parts = fen.Split(' ');
            if (parts.Length < 1) return fen;

            var position = parts[0];
            var ranks = position.Split('/');

            // Mirror the board vertically
            var mirroredRanks = new string[8];
            for (int i = 0; i < 8; i++)
            {
                mirroredRanks[i] = ranks[7 - i];
            }

            // Flip piece colors
            var mirroredPosition = string.Join("/", mirroredRanks);
            var result = "";

            foreach (char c in mirroredPosition)
            {
                if (char.IsLetter(c))
                {
                    result += char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c);
                }
                else
                {
                    result += c;
                }
            }

            // Update the rest of the FEN
            if (parts.Length > 1)
            {
                var activeColor = parts[1] == "w" ? "b" : "w";
                parts[0] = result;
                parts[1] = activeColor;

                // Mirror castling rights
                if (parts.Length > 2)
                {
                    var castling = parts[2];
                    var newCastling = "";
                    foreach (char c in castling)
                    {
                        newCastling += c switch
                        {
                            'K' => 'k',
                            'Q' => 'q',
                            'k' => 'K',
                            'q' => 'Q',
                            _ => c
                        };
                    }
                    parts[2] = newCastling;
                }
            }

            return string.Join(" ", parts);
        }
    }

    public class DebugProgram
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Meridian Chess Engine - Debug Mode");
            Console.WriteLine("===================================");

            try
            {
                // Test NNUE loading
                Console.WriteLine("Testing NNUE loading...");
                var nnuePath = "networks/obsidian.nnue";
                if (File.Exists(nnuePath))
                {
                    Console.WriteLine($"NNUE file found: {nnuePath}");
                    if (Evaluator.LoadNNUE(nnuePath))
                    {
                        Console.WriteLine("NNUE loaded successfully");
                    }
                    else
                    {
                        Console.WriteLine("NNUE loading failed");
                    }
                }
                else
                {
                    Console.WriteLine($"NNUE file not found: {nnuePath}");
                }

                Console.WriteLine();

                // Debug starting position
                DebugEvaluator.DebugOpeningMoves();

                // Test specific problematic positions
                Console.WriteLine("\nTesting specific positions:");

                // Test position after 1.a3
                var testFen = "rnbqkbnr/pppppppp/8/8/8/P7/1PPPPPPP/RNBQKBNR b KQkq - 0 1";
                var testPos = Position.FromFen(testFen);
                if (testPos.IsOk)
                {
                    Console.WriteLine($"\nPosition after 1.a3:");
                    DebugEvaluator.DebugPosition(testPos.Value);
                }

                // Interactive mode
                Console.WriteLine("\nEnter FEN positions to debug (or 'quit' to exit):");
                while (true)
                {
                    Console.Write("FEN> ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
                        break;

                    var pos = Position.FromFen(input);
                    if (pos.IsOk)
                    {
                        DebugEvaluator.DebugPosition(pos.Value);
                    }
                    else
                    {
                        Console.WriteLine("Invalid FEN position");
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("Debug session ended.");
        }
    }
}
