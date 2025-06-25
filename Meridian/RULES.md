# rules.md - C# Coding Conventions for UCI Chess Engine Development

## Overview

This document defines comprehensive coding standards and best practices for developing UCI-compliant chess engines in C#. These rules ensure code quality, performance, maintainability, and UCI protocol compliance.

## Table of Contents

1. [Naming Conventions](#naming-conventions)
2. [Code Formatting](#code-formatting)
3. [File Organization](#file-organization)
4. [UCI Protocol Implementation](#uci-protocol-implementation)
5. [Chess Engine Specific Patterns](#chess-engine-specific-patterns)
6. [Performance Guidelines](#performance-guidelines)
7. [Testing Requirements](#testing-requirements)
8. [Documentation Standards](#documentation-standards)
9. [Security and Input Validation](#security-and-input-validation)

## Naming Conventions

### Type Names
- **Classes**: Use PascalCase (e.g., `ChessEngine`, `MoveGenerator`, `TranspositionTable`)
- **Interfaces**: Prefix with "I" and use PascalCase (e.g., `IPositionEvaluator`, `ISearchAlgorithm`)
- **Structs**: Use PascalCase for value types (e.g., `Move`, `Square`, `Bitboard`)
- **Enums**: Use singular names for non-flags, plural for flags (e.g., `PieceType`, `CastlingRights`)

```csharp
// Good examples
public class AlphaBetaSearcher { }
public interface IChessEngine { }
public struct Move { }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }

[Flags]
public enum CastlingRights { None = 0, WhiteKingside = 1, WhiteQueenside = 2 }
```

### Members
- **Methods**: PascalCase with verb-based names (e.g., `GenerateMoves()`, `EvaluatePosition()`)
- **Properties**: PascalCase with noun-based names (e.g., `ActivePlayer`, `BoardState`)
- **Fields**: 
  - Private: `_camelCase` with underscore prefix
  - Static private: `s_camelCase` 
  - Thread-static: `t_camelCase`
- **Parameters and local variables**: camelCase
- **Constants**: PascalCase

```csharp
public class ChessEngine
{
    private readonly IPositionEvaluator _evaluator;
    private static readonly Dictionary<ulong, int> s_transpositionTable;
    [ThreadStatic]
    private static TimeSpan t_searchTime;
    
    public const int MaxSearchDepth = 64;
    
    public Move FindBestMove(Position position, int depth) { }
}
```

## Code Formatting

### Indentation and Spacing
- Use 4 spaces for indentation (no tabs)
- One statement per line
- Space after keywords: `if (`, `for (`, `while (`
- Spaces around binary operators: `a + b`, `x == y`

### Braces
- Opening braces on new line (Allman style)
- Always use braces for control structures
- Empty blocks may use concise form: `{}`

```csharp
public void GenerateMoves(Position position)
{
    if (position.IsValid)
    {
        foreach (var piece in position.Pieces)
        {
            // Generate moves
        }
    }
}
```

### Line Length
- Maximum 120 characters per line
- Break long method signatures appropriately

## File Organization

### Project Structure
```
ChessEngine/
├── src/
│   ├── Core/
│   │   ├── Board/
│   │   │   ├── Bitboard.cs
│   │   │   ├── Position.cs
│   │   │   └── Square.cs
│   │   └── Moves/
│   │       ├── Move.cs
│   │       └── MoveList.cs
│   ├── UCI/
│   │   ├── UCIProtocol.cs
│   │   ├── Commands/
│   │   └── Responses/
│   ├── Search/
│   │   ├── AlphaBeta.cs
│   │   ├── TranspositionTable.cs
│   │   └── TimeManager.cs
│   ├── Evaluation/
│   │   └── Evaluator.cs
│   └── Generation/
│       └── MoveGenerator.cs
└── tests/
    ├── Perft.Tests/
    ├── UCI.Tests/
    └── Search.Tests/
```

### Using Directives
- Place at top of file, outside namespace
- System namespaces first, then alphabetically
- Remove unused usings

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChessEngine.Core;
using ChessEngine.Search;
```

## UCI Protocol Implementation

### Command Processing
- Implement asynchronous command processing
- Use thread-safe communication patterns
- Validate all input commands

```csharp
public class UCIProtocol
{
    private readonly ConcurrentQueue<string> _commandQueue = new();
    
    public async Task<UCIResponse> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        // Validate command format
        if (!IsValidCommand(command))
            throw new ArgumentException($"Invalid UCI command: {command}");
            
        // Process asynchronously with timeout
        using var cts = new CancellationTokenSource(timeoutMs);
        return await ProcessCommandAsync(command, cts.Token);
    }
}
```

### State Management
- Maintain clear engine states
- Implement proper synchronization
- Handle all UCI commands appropriately

```csharp
public enum UCIEngineState
{
    Uninitialized,
    Initializing,
    Ready,
    Thinking,
    Pondering,
    Stopped
}
```

### Error Handling
- Never let exceptions escape to UCI interface
- Log errors appropriately
- Return valid UCI responses even on error

## Chess Engine Specific Patterns

### Bitboard Implementation
- Use `ulong` (System.UInt64) as base type
- Implement as readonly struct for immutability
- Overload operators for clean syntax

```csharp
public readonly struct Bitboard : IEquatable<Bitboard>
{
    private readonly ulong _bits;
    
    public Bitboard(ulong bits) => _bits = bits;
    
    public static Bitboard operator &(Bitboard a, Bitboard b) => new(a._bits & b._bits);
    public static Bitboard operator |(Bitboard a, Bitboard b) => new(a._bits | b._bits);
    public static Bitboard operator ~(Bitboard a) => new(~a._bits);
    
    public bool Equals(Bitboard other) => _bits == other._bits;
}
```

### Move Representation
- Pack move data efficiently (32 bits typical)
- Use readonly struct for moves
- Include from, to, type, and promotion info

```csharp
public readonly struct Move
{
    private readonly uint _data;
    
    // Bit layout: [from:6][to:6][type:4][promotion:4][flags:12]
    public int From => (int)(_data & 0x3F);
    public int To => (int)((_data >> 6) & 0x3F);
    public MoveType Type => (MoveType)((_data >> 12) & 0xF);
}
```

### Position Representation
- Use piece-centric bitboards
- Maintain incremental Zobrist hash
- Track game state (castling, en passant, etc.)

```csharp
public class Position
{
    // Bitboards for each piece type
    private ulong _whitePawns;
    private ulong _whiteKnights;
    // ... other pieces
    
    // Game state
    public Color ActivePlayer { get; private set; }
    public CastlingRights CastlingRights { get; private set; }
    public Square? EnPassantSquare { get; private set; }
    public int HalfmoveClock { get; private set; }
    
    // Zobrist hash for transposition table
    public ulong Hash { get; private set; }
}
```

## Performance Guidelines

### Memory Management
- Use object pooling for frequently allocated objects
- Prefer stackalloc for small temporary arrays
- Minimize garbage collection pressure

```csharp
public class MoveListPool
{
    private readonly ObjectPool<MoveList> _pool = new();
    
    public MoveList Rent() => _pool.Get();
    public void Return(MoveList list) => _pool.Return(list);
}
```

### Hot Path Optimizations
- Mark critical methods with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- Use ref returns for large structs
- Avoid boxing in performance-critical code
- Prefer switch expressions over if-else chains

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static int PopCount(ulong bitboard)
{
    return (int)System.Numerics.BitOperations.PopCount(bitboard);
}
```

### Search Optimization
- Implement iterative deepening
- Use principal variation search
- Apply null move pruning carefully
- Maintain killer and history heuristics

### Transposition Table
- Use power-of-2 size for fast indexing
- Implement always-replace strategy
- Store bound type (exact/lower/upper)

```csharp
public class TranspositionTable
{
    private readonly Entry[] _entries;
    private readonly ulong _indexMask;
    
    private struct Entry
    {
        public ulong Key;
        public short Score;
        public byte Depth;
        public byte Flags; // NodeType + Age
        public Move BestMove;
    }
    
    public void Store(ulong key, int score, int depth, NodeType type, Move bestMove)
    {
        var index = key & _indexMask;
        _entries[index] = new Entry
        {
            Key = key,
            Score = (short)score,
            Depth = (byte)depth,
            Flags = (byte)type,
            BestMove = bestMove
        };
    }
}
```

## Testing Requirements

### Move Generation Testing
- Implement comprehensive Perft tests
- Test all special moves (castling, en passant, promotion)
- Validate make/unmake consistency

```csharp
[Test]
public void Perft_StartingPosition_Depth6()
{
    var position = Position.StartingPosition();
    var nodeCount = Perft(position, 6);
    Assert.AreEqual(119_060_324, nodeCount);
}
```

### UCI Compliance Testing
- Test all mandatory UCI commands
- Validate time management
- Test option handling
- Verify output format compliance

### Performance Benchmarks
- Maintain performance regression tests
- Track nodes per second (NPS)
- Monitor memory usage
- Test under various time controls

### Test Suites
- Use standard test positions (WAC, STS, etc.)
- Implement EPD/FEN test runner
- Track solving percentage over time

## Documentation Standards

### XML Documentation
- Document all public APIs
- Include parameter descriptions
- Provide usage examples
- Document exceptions

```csharp
/// <summary>
/// Searches for the best move in the given position using iterative deepening.
/// </summary>
/// <param name="position">The chess position to analyze.</param>
/// <param name="timeLimit">Maximum time to spend searching.</param>
/// <param name="cancellationToken">Token to cancel the search.</param>
/// <returns>The best move found within the time limit.</returns>
/// <exception cref="ArgumentNullException">Thrown when position is null.</exception>
/// <example>
/// <code>
/// var engine = new ChessEngine();
/// var move = await engine.SearchAsync(position, TimeSpan.FromSeconds(5));
/// </code>
/// </example>
public async Task<Move> SearchAsync(Position position, TimeSpan timeLimit, 
    CancellationToken cancellationToken = default)
```

### Code Comments
- Explain complex algorithms
- Document magic numbers
- Clarify chess-specific logic
- Keep comments up-to-date

### README Requirements
- Clear project description
- Installation instructions
- UCI command reference
- Performance characteristics
- Contributing guidelines

## Security and Input Validation

### UCI Input Validation
- Validate all UCI commands before processing
- Sanitize FEN strings
- Check move format validity
- Handle malformed input gracefully

```csharp
public static bool IsValidFEN(string fen)
{
    if (string.IsNullOrWhiteSpace(fen))
        return false;
        
    var parts = fen.Split(' ');
    if (parts.Length != 6)
        return false;
        
    // Validate each FEN component
    return IsValidPiecePlacement(parts[0]) &&
           IsValidActiveColor(parts[1]) &&
           IsValidCastlingRights(parts[2]) &&
           IsValidEnPassant(parts[3]) &&
           IsValidHalfmoveClock(parts[4]) &&
           IsValidFullmoveNumber(parts[5]);
}
```

### Resource Management
- Implement proper disposal patterns
- Use using statements for resources
- Limit memory allocation sizes
- Validate array bounds

### Thread Safety
- Protect shared state with appropriate synchronization
- Use concurrent collections where appropriate
- Avoid race conditions in search
- Implement proper cancellation

## Static Analysis Rules

### Enforce with Datadog/SonarQube
- No forced garbage collection (`GC.Collect()`)
- Proper exception handling (throw, don't just create)
- Resource disposal compliance
- Integer overflow prevention
- Thread safety verification

### Code Quality Metrics
- Maintain cyclomatic complexity < 10
- Keep method length < 50 lines
- Ensure test coverage > 80%
- Monitor technical debt

## Performance Targets

### Minimum Requirements
- Perft(6) from start: < 10 seconds
- NPS: > 1 million on modern hardware
- Move generation: < 1 microsecond average
- Evaluation: < 500 nanoseconds

### Optimization Priorities
1. Move generation efficiency
2. Evaluation function speed
3. Search algorithm effectiveness
4. Memory access patterns
5. Cache utilization

## Conclusion

These rules provide a comprehensive framework for developing high-quality, performant UCI-compliant chess engines in C#. Following these conventions ensures code maintainability, reliability, and competitive performance while adhering to industry standards and best practices.
