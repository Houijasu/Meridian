# NNUE Implementation Guide for Meridian Chess Engine

## Overview

This document provides a comprehensive guide to the NNUE (Efficiently Updatable Neural Networks) implementation in the Meridian chess engine. The implementation has been completely rewritten to follow standard NNUE architecture and provide proper evaluation functionality.

## Architecture

### Network Structure

The NNUE network follows a standard multi-layer architecture:

```
Input Layer (768 features) -> Feature Weights -> L1 (256 neurons)
                                    |
                                    v
                              L1 -> L2 (32 neurons)
                                    |
                                    v
                              L2 -> L3 (32 neurons)
                                    |
                                    v
                              L3 -> Output (1 value)
```

### Key Components

1. **Feature Extraction**: Converts board position to 768-dimensional feature vector
2. **Accumulator**: Efficiently tracks L1 activations with incremental updates
3. **Multi-layer Forward Pass**: Processes activations through L2, L3, and output layers
4. **Quantization**: Uses proper quantization for performance and accuracy

## Implementation Details

### Constants (`NNUEConstants.cs`)

```csharp
public const int InputDimensions = 768;     // 12 pieces * 64 squares
public const int L1Size = 256;              // First hidden layer
public const int L2Size = 32;               // Second hidden layer
public const int L3Size = 32;               // Third hidden layer
public const int OutputDimensions = 1;      // Single evaluation output

// Quantization parameters
public const int QA = 255;                  // Input quantization
public const int QB = 64;                   // Hidden layer quantization
public const int QAB = QA * QB;             // Combined quantization
public const int ScaleFactor = 400;         // Final output scaling
```

### Network Architecture (`NNUENetwork.cs`)

#### Memory Layout

- **Feature Weights**: `short[GetFeatureWeightsSize()]` - Input to L1 transformation
- **Feature Bias**: `short[L1Size]` - L1 layer bias
- **L1 Weights**: `sbyte[L1Size * L2Size]` - L1 to L2 transformation
- **L1 Bias**: `int[L2Size]` - L2 layer bias
- **L2 Weights**: `sbyte[L2Size * L3Size]` - L2 to L3 transformation
- **L2 Bias**: `int[L3Size]` - L3 layer bias
- **L3 Weights**: `sbyte[L3Size * OutputDimensions]` - L3 to output transformation
- **L3 Bias**: `int[OutputDimensions]` - Output layer bias

#### Accumulator System

The accumulator system provides efficient incremental updates:

```csharp
public class Accumulator
{
    private readonly short[][] _accumulation;  // [2][L1Size] for both perspectives
    private readonly bool[] _computed;          // Track computation state
}
```

**Key Features:**
- **Dual Perspective**: Maintains separate accumulators for white and black views
- **SIMD Optimization**: Uses AVX2/SSE2 instructions when available
- **Incremental Updates**: Only updates changed features, not full board
- **Copy-on-Write**: Efficient accumulator stack management

### Feature Indexing

#### Piece-Square Encoding

Features are encoded using the HalfKP (Half-King-Piece) scheme:

```csharp
public static int GetFeatureIndex(int pieceType, int square, bool isWhite)
{
    int colorOffset = isWhite ? 0 : PieceTypes * Squares;
    return colorOffset + pieceType * Squares + square;
}
```

#### King Bucket System

King position influences feature weights through bucketing:

```csharp
public static int GetKingBucket(int kingSquare)
{
    int file = kingSquare % 8;
    int rank = kingSquare / 8;
    
    if (rank <= 1) // Back two ranks
        return file <= 3 ? 0 : 1;
    else // Front six ranks
        return file <= 3 ? 2 : 3;
}
```

**Bucket Layout:**
- Bucket 0: Back ranks, queen-side (a1-d1, a2-d2)
- Bucket 1: Back ranks, king-side (e1-h1, e2-h2)
- Bucket 2: Front ranks, queen-side (a3-d8, a4-d8)
- Bucket 3: Front ranks, king-side (e3-h8, e4-h8)

### Evaluation Process

#### Forward Pass

1. **L1 Activation**: Use accumulator values with ClippedReLU activation
2. **L2 Forward**: `L2[i] = ClippedReLU(Σ(L1[j] * W1[i,j]) + B1[i])`
3. **L3 Forward**: `L3[i] = ClippedReLU(Σ(L2[j] * W2[i,j]) + B2[i])`
4. **Output**: `Output = Σ(L3[i] * W3[i]) + B3`

#### Quantization

```csharp
public static int ClippedReLU(int value)
{
    return Math.Max(0, Math.Min(127, value));
}
```

Final evaluation scaling:
```csharp
int evaluation = output * ScaleFactor / QAB;
```

## Network File Format

### Obsidian Compatibility

The implementation supports Obsidian-format NNUE files:

```
Header (1024 bytes) - Skipped
Feature Weights (KingBuckets * InputDimensions * L1Size * 2 bytes)
Feature Biases (L1Size * 2 bytes)
L1 Weights (L1Size * L2Size * 1 byte)
L1 Biases (L2Size * 4 bytes)
L2 Weights (L2Size * L3Size * 1 byte)
L2 Biases (L3Size * 4 bytes)
L3 Weights (L3Size * OutputDimensions * 1 byte)
L3 Biases (OutputDimensions * 4 bytes)
```

### Loading Process

```csharp
public bool LoadNetwork(string path)
{
    // 1. Skip header
    stream.Seek(ObsidianHeaderSize, SeekOrigin.Begin);
    
    // 2. Load feature weights and biases
    LoadFeatureWeights(reader);
    LoadFeatureBiases(reader);
    
    // 3. Load L1 layer
    LoadL1Weights(reader);
    LoadL1Biases(reader);
    
    // 4. Load L2 layer
    LoadL2Weights(reader);
    LoadL2Biases(reader);
    
    // 5. Load L3 layer (output)
    LoadL3Weights(reader);
    LoadL3Biases(reader);
    
    IsLoaded = true;
    return true;
}
```

## Performance Optimizations

### SIMD Instructions

The accumulator uses SIMD instructions for faster vector operations:

```csharp
if (Avx2.IsSupported)
{
    // Process 16 elements at once
    var accVec = Avx.LoadVector256(acc + i * 16);
    var weightVec = Avx.LoadVector256(weights + i * 16);
    var result = Avx2.Add(accVec, weightVec);
    Avx.Store(acc + i * 16, result);
}
```

### Incremental Updates

Instead of full board evaluation, the system uses incremental updates:

```csharp
public void UpdateAccumulator(Position position, Move move)
{
    // Only update features that changed
    RemovePieceFromAccumulator(/* old position */);
    AddPieceToAccumulator(/* new position */);
    
    // Handle captures
    if (capturedPiece.HasValue)
        RemovePieceFromAccumulator(/* captured piece */);
        
    // Handle special moves (castling, en passant)
    if (move.IsCastling)
        HandleCastling(/* rook movement */);
}
```

### Memory Management

- **Pre-allocated Buffers**: All computation buffers are pre-allocated
- **Stack-based Accumulators**: 256-element accumulator stack for search
- **Zero-copy Operations**: Efficient accumulator copying

## Usage Examples

### Basic Setup

```csharp
var network = new NNUENetwork();
var evaluator = new Evaluator();

// Load network
if (network.LoadNetwork("path/to/network.nnue"))
{
    evaluator.LoadNNUE("path/to/network.nnue");
    Console.WriteLine("NNUE loaded successfully");
}
```

### Position Evaluation

```csharp
var position = new Position(); // Starting position
evaluator.InitializeNNUE(position);

// Evaluate position
int evaluation = evaluator.Evaluate(position);
Console.WriteLine($"Evaluation: {evaluation} centipawns");
```

### Move Updates

```csharp
var move = new Move(Square.e2, Square.e4, MoveType.Normal);
position.MakeMove(move);
evaluator.UpdateNNUE(position, move);

// New evaluation reflects the move
int newEvaluation = evaluator.Evaluate(position);
```

## Testing and Validation

### Unit Tests

The implementation includes comprehensive unit tests:

```csharp
[TestMethod]
public void TestFeatureIndexing()
{
    Assert.AreEqual(0, NNUEConstants.GetPieceTypeIndex(PieceType.Pawn));
    Assert.AreEqual(5, NNUEConstants.GetPieceTypeIndex(PieceType.King));
}

[TestMethod]
public void TestEvaluationConsistency()
{
    var eval1 = network.Evaluate(position);
    var eval2 = network.Evaluate(position);
    Assert.AreEqual(eval1, eval2);
}
```

### Performance Testing

```csharp
[TestMethod]
[TestCategory("Performance")]
public void TestEvaluationSpeed()
{
    var startTime = DateTime.Now;
    for (int i = 0; i < 100000; i++)
    {
        network.Evaluate(position);
    }
    var elapsed = DateTime.Now - startTime;
    // Should achieve >50k evaluations per second
}
```

## Integration with Search

### Search Integration

```csharp
public class SearchEngine
{
    private readonly Evaluator _evaluator;
    
    public int AlphaBeta(Position position, int depth, int alpha, int beta)
    {
        if (depth == 0)
            return _evaluator.Evaluate(position);
            
        foreach (var move in GenerateMoves(position))
        {
            position.MakeMove(move);
            _evaluator.UpdateNNUE(position, move);
            
            int score = -AlphaBeta(position, depth - 1, -beta, -alpha);
            
            position.UnmakeMove(move);
            // Accumulator automatically reverts with position stack
            
            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        
        return alpha;
    }
}
```

## Troubleshooting

### Common Issues

1. **Network Loading Fails**
   - Check file path and permissions
   - Verify file format matches expected structure
   - Ensure file size is reasonable (>1MB, <100MB)

2. **Evaluation Returns 0**
   - Network may not be loaded
   - Check `IsLoaded` property
   - Verify network file integrity

3. **Poor Performance**
   - Ensure SIMD instructions are enabled
   - Check accumulator update frequency
   - Verify no unnecessary full refreshes

### Debug Output

```csharp
// Enable debug output
Console.WriteLine($"NNUE loaded: {network.IsLoaded}");
Console.WriteLine($"Expected file size: {NNUEConstants.ExpectedFileSize}");
Console.WriteLine($"Actual file size: {new FileInfo(path).Length}");

// Check accumulator state
accumulator.PrintAccumulation(0, 10);
Console.WriteLine($"Accumulator sum: {accumulator.GetAccumulationSum(0)}");
```

## Future Improvements

### Planned Features

1. **Multi-format Support**: Support for Stockfish and other NNUE formats
2. **Dynamic Network Loading**: Runtime network switching
3. **Custom Training**: Integration with training pipeline
4. **Advanced Features**: Support for newer NNUE architectures

### Performance Optimizations

1. **ARM NEON**: Support for ARM processors
2. **GPU Acceleration**: CUDA/OpenCL support for evaluation
3. **Batch Processing**: Evaluate multiple positions simultaneously
4. **Quantization**: INT8 and lower precision support

## Conclusion

The NNUE implementation in Meridian provides a robust, efficient, and accurate neural network evaluation system. It follows standard NNUE architecture while providing optimizations specific to the engine's needs. The implementation is designed to be maintainable, testable, and extensible for future improvements.

For questions or issues, refer to the unit tests and diagnostic methods provided in the codebase.