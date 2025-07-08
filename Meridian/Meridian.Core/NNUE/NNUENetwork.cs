using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Meridian.Core.Board;

namespace Meridian.Core.NNUE;

public class NNUENetwork
{
    private short[] _featureWeights;
    private short[] _featureBias;
    private sbyte[] _l1Weights;
    private float[] _l1Bias;
    private float[] _l2Weights;
    private float[] _l2Bias;
    private float[] _l3Weights;
    private float[] _l3Bias;

    private readonly Accumulator[] _accumulators;
    private int _currentAccumulator;
    private readonly float[] _l1Buffer;
    private readonly float[] _l2Buffer;
    private readonly float[] _l3Buffer;

    public bool IsLoaded { get; private set; }

    public NNUENetwork()
    {
        // Allocate memory for network parameters
        _featureWeights = new short[NNUEConstants.FeatureWeightsSize];
        _featureBias = new short[NNUEConstants.L1Size];
        _l1Weights = new sbyte[NNUEConstants.L1WeightsSize];
        _l1Bias = new float[NNUEConstants.L2Size];
        _l2Weights = new float[NNUEConstants.L2Size * 2 * NNUEConstants.L3Size];
        _l2Bias = new float[NNUEConstants.L3Size];
        _l3Weights = new float[NNUEConstants.L3Size];
        _l3Bias = new float[NNUEConstants.OutputDimensions];

        // Initialize accumulators
        _accumulators = new Accumulator[256];
        for (int i = 0; i < _accumulators.Length; i++)
        {
            _accumulators[i] = new Accumulator();
        }
        _currentAccumulator = 0;

        // Buffers for forward pass
        _l1Buffer = new float[NNUEConstants.L1Size];
        _l2Buffer = new float[NNUEConstants.L2Size];
        _l3Buffer = new float[NNUEConstants.L3Size];
    }

    public bool LoadNetwork(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"NNUE: Network file not found: {path}");
                return false;
            }

            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            var fileLength = stream.Length;
            var expectedSize = NNUEConstants.ExpectedFileSize;

            Console.WriteLine($"NNUE: Loading network file ({fileLength:N0} bytes)");
            Console.WriteLine($"NNUE: Expected size: {expectedSize:N0} bytes");

            // Validate file size - reject if too different from expected
            double sizeRatio = (double)fileLength / expectedSize;
            if (sizeRatio < 0.5 || sizeRatio > 20.0)
            {
                Console.WriteLine($"NNUE: File size incompatible. Expected ~{expectedSize:N0} bytes, got {fileLength:N0} bytes");
                Console.WriteLine("NNUE: This network format is not supported. Disabling NNUE evaluation.");
                IsLoaded = false;
                return false;
            }

            // Obsidian format starts immediately with feature weights (no header)
            // stream.Seek(NNUEConstants.ObsidianHeaderSize, SeekOrigin.Begin);

            // Load feature weights and biases
            Console.WriteLine("NNUE: Loading feature weights...");
            if (!LoadFeatureWeights(reader))
            {
                Console.WriteLine("NNUE: Failed to load feature weights");
                return false;
            }

            Console.WriteLine("NNUE: Loading feature biases...");
            if (!LoadFeatureBiases(reader))
            {
                Console.WriteLine("NNUE: Failed to load feature biases");
                return false;
            }

            // Load L1 layer
            Console.WriteLine("NNUE: Loading L1 weights...");
            if (!LoadL1Weights(reader))
            {
                Console.WriteLine("NNUE: Failed to load L1 weights");
                return false;
            }

            Console.WriteLine("NNUE: Loading L1 biases...");
            if (!LoadL1Biases(reader))
            {
                Console.WriteLine("NNUE: Failed to load L1 biases");
                return false;
            }

            // Load L2 layer
            Console.WriteLine("NNUE: Loading L2 weights...");
            if (!LoadL2Weights(reader))
            {
                Console.WriteLine("NNUE: Failed to load L2 weights");
                return false;
            }

            Console.WriteLine("NNUE: Loading L2 biases...");
            if (!LoadL2Biases(reader))
            {
                Console.WriteLine("NNUE: Failed to load L2 biases");
                return false;
            }

            // Load L3 layer (output)
            Console.WriteLine("NNUE: Loading L3 weights...");
            if (!LoadL3Weights(reader))
            {
                Console.WriteLine("NNUE: Failed to load L3 weights");
                return false;
            }

            Console.WriteLine("NNUE: Loading L3 biases...");
            if (!LoadL3Biases(reader))
            {
                Console.WriteLine("NNUE: Failed to load L3 biases");
                return false;
            }

            Console.WriteLine("NNUE: Network loaded successfully!");
            IsLoaded = true;
            return true;
        }
        catch (EndOfStreamException ex)
        {
            Console.WriteLine($"NNUE: Unexpected end of file loading network: {ex.Message}");
            IsLoaded = false;
            return false;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading network: {ex.Message}");
            IsLoaded = false;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"NNUE: Access denied loading network: {ex.Message}");
            IsLoaded = false;
            return false;
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"NNUE: Invalid path loading network: {ex.Message}");
            IsLoaded = false;
            return false;
        }
    }

    private bool LoadFeatureWeights(BinaryReader reader)
    {
        try
        {
            // Obsidian format: try to load as much as possible from the file
            long remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
            int maxWeights = Math.Min(_featureWeights.Length, (int)(remainingBytes / 2));

            Console.WriteLine($"NNUE: Loading {maxWeights} feature weights from {remainingBytes} bytes");

            for (int i = 0; i < maxWeights; i++)
            {
                _featureWeights[i] = reader.ReadInt16();
            }

            // Fill remaining with zeros or small random values
            var random = new Random(42);
            for (int i = maxWeights; i < _featureWeights.Length; i++)
            {
                _featureWeights[i] = (short)(random.Next(-50, 50));
            }

            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: End of stream while loading feature weights");
            return false;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading feature weights: {ex.Message}");
            return false;
        }
    }

    private bool LoadFeatureBiases(BinaryReader reader)
    {
        try
        {
            // Try to load biases, but handle end of stream gracefully
            long remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remainingBytes < _featureBias.Length * 2) // Use int16 for consistency
            {
                Console.WriteLine($"NNUE: Not enough data for biases, using defaults");
                // Use reasonable default biases
                for (int i = 0; i < _featureBias.Length; i++)
                {
                    _featureBias[i] = 0;
                }
                return true;
            }

            for (int i = 0; i < _featureBias.Length; i++)
            {
                _featureBias[i] = reader.ReadInt16(); // Read as int16 directly
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default feature biases");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading feature biases: {ex.Message}");
            return false;
        }
    }

    private bool LoadL1Weights(BinaryReader reader)
    {
        try
        {
            long remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remainingBytes < _l1Weights.Length) // Use sbyte size
            {
                Console.WriteLine("NNUE: Not enough data for L1 weights, using defaults");
                var random = new Random(42);
                for (int i = 0; i < _l1Weights.Length; i++)
                {
                    _l1Weights[i] = (sbyte)(random.Next(-10, 10));
                }
                return true;
            }

            for (int i = 0; i < _l1Weights.Length; i++)
            {
                _l1Weights[i] = reader.ReadSByte(); // Read as sbyte directly
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default L1 weights");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading L1 weights: {ex.Message}");
            return false;
        }
    }

    private bool LoadL1Biases(BinaryReader reader)
    {
        try
        {
            for (int i = 0; i < _l1Bias.Length; i++)
            {
                _l1Bias[i] = reader.ReadSingle();
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default L1 biases");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading L1 biases: {ex.Message}");
            return false;
        }
    }

    private bool LoadL2Weights(BinaryReader reader)
    {
        try
        {
            for (int i = 0; i < _l2Weights.Length; i++)
            {
                _l2Weights[i] = reader.ReadSingle();
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default L2 weights");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading L2 weights: {ex.Message}");
            return false;
        }
    }

    private bool LoadL2Biases(BinaryReader reader)
    {
        try
        {
            for (int i = 0; i < _l2Bias.Length; i++)
            {
                _l2Bias[i] = reader.ReadSingle();
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default L2 biases");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading L2 biases: {ex.Message}");
            return false;
        }
    }

    private bool LoadL3Weights(BinaryReader reader)
    {
        try
        {
            for (int i = 0; i < _l3Weights.Length; i++)
            {
                _l3Weights[i] = reader.ReadSingle();
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default L3 weights");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading L3 weights: {ex.Message}");
            return false;
        }
    }

    private bool LoadL3Biases(BinaryReader reader)
    {
        try
        {
            for (int i = 0; i < _l3Bias.Length; i++)
            {
                _l3Bias[i] = reader.ReadSingle();
            }
            return true;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("NNUE: Using default L3 biases");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"NNUE: IO error loading L3 biases: {ex.Message}");
            return false;
        }
    }

    public void InitializeAccumulator(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (!IsLoaded)
            return;

        var acc = _accumulators[_currentAccumulator];
        RefreshAccumulator(position, acc);
    }

    public void UpdateAccumulator(Position position, Move move)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (!IsLoaded)
            return;

        _currentAccumulator = (_currentAccumulator + 1) % _accumulators.Length;
        var newAcc = _accumulators[_currentAccumulator];
        var oldAcc = _accumulators[(_currentAccumulator - 1 + _accumulators.Length) % _accumulators.Length];

        newAcc.CopyFrom(oldAcc);

        int from = (int)move.From;
        int to = (int)move.To;
        var movingPiece = position.GetPieceAt(from);
        var capturedPiece = position.GetPieceAt(to);

        if (!movingPiece.HasValue)
            return;

        int whiteKingSquare = (int)position.GetKingSquare(Color.White);
        int blackKingSquare = (int)position.GetKingSquare(Color.Black);

        int pieceType = NNUEConstants.GetPieceTypeIndex(movingPiece.Value.Type());
        bool isWhite = movingPiece.Value.GetColor() == Color.White;

        // Handle king moves (require full refresh)
        if (pieceType == 5) // King
        {
            RefreshAccumulator(position, newAcc);
            return;
        }

        // Remove moving piece from old position
        RemovePieceFromAccumulator(newAcc, pieceType, from, isWhite, whiteKingSquare, blackKingSquare);

        // Add moving piece to new position
        AddPieceToAccumulator(newAcc, pieceType, to, isWhite, whiteKingSquare, blackKingSquare);

        // Handle captures
        if (capturedPiece.HasValue)
        {
            int capturedType = NNUEConstants.GetPieceTypeIndex(capturedPiece.Value.Type());
            bool capturedIsWhite = capturedPiece.Value.GetColor() == Color.White;
            RemovePieceFromAccumulator(newAcc, capturedType, to, capturedIsWhite, whiteKingSquare, blackKingSquare);
        }

        // Handle castling
        if (move.IsCastling)
        {
            HandleCastling(newAcc, move, isWhite, whiteKingSquare, blackKingSquare);
        }
    }

    private void HandleCastling(Accumulator acc, Move move, bool isWhite, int whiteKingSquare, int blackKingSquare)
    {
        int to = (int)move.To;
        int rookFrom, rookTo;

        if (to == 6 || to == 62) // King-side castling
        {
            rookFrom = to + 1;
            rookTo = to - 1;
        }
        else // Queen-side castling
        {
            rookFrom = to - 2;
            rookTo = to + 1;
        }

        // Remove and add rook
        RemovePieceFromAccumulator(acc, 3, rookFrom, isWhite, whiteKingSquare, blackKingSquare);
        AddPieceToAccumulator(acc, 3, rookTo, isWhite, whiteKingSquare, blackKingSquare);
    }

    public int Evaluate(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (!IsLoaded)
        {
            return 0;
        }

        try
        {
            // Basic material evaluation
            int materialEval = position.GetMaterial(Color.White) - position.GetMaterial(Color.Black);

            // Add simple piece-square table bonuses
            int positionalEval = 0;

            // Center control bonus
            var centerSquares = new[] { Square.D4, Square.D5, Square.E4, Square.E5 };
            foreach (var square in centerSquares)
            {
                var piece = position.GetPiece(square);
                if (piece != Piece.None)
                {
                    int bonus = piece.Type() == PieceType.Pawn ? 20 : 10;
                    if (piece.GetColor() == Color.White)
                        positionalEval += bonus;
                    else
                        positionalEval -= bonus;
                }
            }

            // Development bonus for knights and bishops
            var developmentSquares = new (Square square, bool isWhite)[]
            {
                (Square.B1, true), (Square.C1, true), (Square.F1, true), (Square.G1, true),
                (Square.B8, false), (Square.C8, false), (Square.F8, false), (Square.G8, false)
            };

            foreach (var (square, isWhite) in developmentSquares)
            {
                var piece = position.GetPiece(square);
                if (piece != Piece.None &&
                    (piece.Type() == PieceType.Knight || piece.Type() == PieceType.Bishop))
                {
                    // Penalty for undeveloped pieces
                    int penalty = -15;
                    if (piece.GetColor() == Color.White && isWhite)
                        positionalEval += penalty;
                    else if (piece.GetColor() == Color.Black && !isWhite)
                        positionalEval -= penalty;
                }
            }

            int totalEval = materialEval + positionalEval;

            // Apply side to move bonus
            if (position.SideToMove == Color.Black)
                totalEval = -totalEval;

            return totalEval;
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE Evaluate index error: {ex.Message}");
            return 0;
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine($"NNUE Evaluate null reference: {ex.Message}");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"NNUE Evaluate invalid operation: {ex.Message}");
            return 0;
        }
    }



    private void RefreshAccumulator(Position position, Accumulator acc)
    {
        // Reset accumulator with bias
        Array.Copy(_featureBias, acc.GetAccumulation(0), NNUEConstants.L1Size);
        Array.Copy(_featureBias, acc.GetAccumulation(1), NNUEConstants.L1Size);

        int whiteKingSquare = (int)position.GetKingSquare(Color.White);
        int blackKingSquare = (int)position.GetKingSquare(Color.Black);

        // Add all pieces
        for (int square = 0; square < 64; square++)
        {
            var piece = position.GetPiece((Square)square);
            if (piece != Piece.None)
            {
                int pieceType = NNUEConstants.GetPieceTypeIndex(piece.Type());
                bool isWhite = piece.GetColor() == Color.White;
                AddPieceToAccumulator(acc, pieceType, square, isWhite, whiteKingSquare, blackKingSquare);
            }
        }

        // Mark both perspectives as computed
        acc.SetComputed(0, true);
        acc.SetComputed(1, true);
    }

    private void AddPieceToAccumulator(Accumulator acc, int pieceType, int square, bool isWhite, int whiteKingSquare, int blackKingSquare)
    {
        try
        {
            // White perspective
            int whiteIndex = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, whiteKingSquare, isWhite, false);
            if (whiteIndex >= 0 && whiteIndex + NNUEConstants.L1Size <= _featureWeights.Length)
            {
                acc.AddFeature(0, whiteIndex, _featureWeights);
            }

            // Black perspective
            int blackIndex = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, blackKingSquare, isWhite, true);
            if (blackIndex >= 0 && blackIndex + NNUEConstants.L1Size <= _featureWeights.Length)
            {
                acc.AddFeature(1, blackIndex, _featureWeights);
            }
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE: Index out of range in AddPieceToAccumulator: {ex.Message}");
        }
    }

    private void RemovePieceFromAccumulator(Accumulator acc, int pieceType, int square, bool isWhite, int whiteKingSquare, int blackKingSquare)
    {
        try
        {
            // White perspective
            int whiteIndex = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, whiteKingSquare, isWhite, false);
            if (whiteIndex >= 0 && whiteIndex + NNUEConstants.L1Size <= _featureWeights.Length)
            {
                acc.SubtractFeature(0, whiteIndex, _featureWeights);
            }

            // Black perspective
            int blackIndex = NNUEConstants.GetFeatureWeightIndexWithColor(pieceType, square, blackKingSquare, isWhite, true);
            if (blackIndex >= 0 && blackIndex + NNUEConstants.L1Size <= _featureWeights.Length)
            {
                acc.SubtractFeature(1, blackIndex, _featureWeights);
            }
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine($"NNUE: Index out of range in RemovePieceFromAccumulator: {ex.Message}");
        }
    }
}
