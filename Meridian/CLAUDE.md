# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Meridian is a high-performance C# UCI-compliant chess engine using .NET 9.0, focused on becoming a competitive chess engine. The project emphasizes bitboard-based move generation, efficient search algorithms, and strict UCI protocol compliance.

## Essential Commands

### Build and Test
```bash
# Build the project
dotnet build Meridian/Meridian.sln

# Run all tests
dotnet test Meridian/Meridian.sln

# Run specific test categories
dotnet test --filter "Category=Perft"  # Move generation validation tests

# Run the engine
dotnet run --project Meridian/Meridian/Meridian

# Create optimized release build
dotnet publish -c Release -r win-x64 --self-contained
```

### Development Workflow
Before committing any move generation changes, ALWAYS run:
```bash
dotnet test --filter "Category=Perft"
```

## Architecture Overview

### Core Components

1. **Bitboard Representation**
   - Uses `ulong` for piece positions
   - Little-Endian Rank-File (LERF) mapping: A1=0, H8=63
   - Implements magic bitboards for sliding piece moves
   - Hardware intrinsics for performance (Popcnt, Tzcnt, Pext)

2. **Move Generation**
   - Staged generation for efficiency
   - Validates with comprehensive Perft tests
   - Special move handling: castling, en passant, promotions

3. **Search Algorithm**
   - Negamax with alpha-beta pruning
   - Transposition table with Zobrist hashing
   - Move ordering: TT move → captures (MVV-LVA) → killers → history
   - Implements null move pruning, LMR, and quiescence search

4. **UCI Protocol**
   - Asynchronous command processing on separate thread
   - Must respond to 'stop' within 50ms
   - Never throw exceptions to GUI
   - Thread-safe state management

### Directory Structure
```
Meridian/
├── Meridian.Core/
│   ├── Board/          # Bitboard, Position, Move structs
│   ├── MoveGeneration/ # Move generator, magic bitboards
│   ├── Search/         # Search engine, transposition table
│   ├── Evaluation/     # Position evaluation
│   └── Protocol/UCI/   # UCI protocol implementation
├── Meridian.Tests/
│   ├── Perft/          # Move generation correctness
│   ├── UCI/            # Protocol compliance
│   └── Search/         # Algorithm validation
└── Meridian/
    └── Program.cs      # Entry point
```

## Critical Performance Requirements

- **Move generation**: < 1 microsecond per position
- **Evaluation**: < 500 nanoseconds per position  
- **Make/Unmake**: < 50 nanoseconds per move
- **Perft(6) from start**: < 10 seconds
- **NPS**: > 1 million on modern hardware

## Key Implementation Guidelines

### Memory Management
- NO allocations in hot paths (search/evaluation)
- Use `stackalloc` for temporary arrays
- Implement make/unmake pattern (never copy board state)
- Use object pooling for move lists

### Thread Safety
- Transposition table must be lock-free
- Use `Interlocked` for statistics
- Each search thread has own stack
- Implement Lazy SMP for parallel search

### Testing Requirements
- Every move generation change MUST pass full Perft suite
- Test special positions: starting position, Kiwipete, etc.
- Maintain > 80% test coverage
- Run EPD test suites (WAC, ECM, STS)

### Code Style
- Enable nullable reference types: `#nullable enable`
- Use file-scoped namespaces
- Private fields: `_camelCase`
- Static fields: `s_camelCase`
- NO comments unless specifically requested
- Zero warnings policy

## Common Pitfalls to Avoid

1. **NEVER use LINQ in search** - causes allocations
2. **Don't copy position during search** - use make/unmake
3. **Always handle time pressure** - must move before timeout
4. **Follow UCI spec exactly** - some GUIs are strict
5. **Profile before optimizing** - avoid premature optimization

## Development Process

1. Run Perft tests before ANY move generation commits
2. Profile performance changes with benchmarks
3. Maintain UCI backward compatibility
4. Use git commit format: `type(scope): description`

## Advanced Implementation Details

### Bitboard Implementation
```csharp
public readonly struct Bitboard : IEquatable<Bitboard>
{
    private readonly ulong _value;
    
    public static int PopCount(Bitboard bb) =>
        BitOperations.PopCount(bb._value);
}
```

### Move Representation (32-bit packed struct)
- From: 6 bits
- To: 6 bits
- Flags: 4 bits
- Captured piece: 4 bits
- Promotion piece: 4 bits
- Reserved: 8 bits

### Search Enhancements Priority
1. Iterative deepening
2. Aspiration windows
3. Principal variation search
4. Null move pruning with verification
5. Late move reductions (LMR)
6. Singular extensions
7. Futility pruning
8. History heuristic

### UCI State Management
```
States: Uninitialized → Initializing → Ready → Thinking/Pondering → Stopped
```

## Performance Optimization Checklist

- [ ] Use hardware intrinsics for bitboard operations
- [ ] Apply `[SkipLocalsInit]` to hot methods
- [ ] Implement staged move generation
- [ ] Use `Span<T>` and `stackalloc` for temporary data
- [ ] Profile with PerfView or similar tools
- [ ] Track NPS for each version
- [ ] Compare against reference engines

## Code Analysis Rules for Performance

### CA1711 - Type Name Suffixes
- Avoid "Flags" suffix unless type is a flags enum
- Use specific suffixes only when extending corresponding base types
- For chess engine: rename MoveFlags to MoveType or MoveKind

### CA2225 - Operator Overload Alternatives
- Provide named methods for operators (for language interoperability)
- Can suppress for performance-critical structs like Bitboard
- Add pragma: `#pragma warning disable CA2225`

### CA1062 - Validate Arguments
- Check public method parameters for null
- For hot paths, consider:
  - Making methods internal/private
  - Using nullable reference types for compile-time safety
  - Suppressing with justification for validated paths

### CA1305 - Culture-Specific Operations
- Use `CultureInfo.InvariantCulture` for parsing/formatting
- Critical for consistent FEN parsing across locales
- Example: `int.Parse(str, CultureInfo.InvariantCulture)`

## Suppression Strategy for Performance

For performance-critical code, suppress warnings with justification:
```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("Category", "Rule", 
    Justification = "Performance-critical hot path")]
```

Or globally in .editorconfig:
```
# Suppress for performance-critical value types
dotnet_diagnostic.CA2225.severity = none
```

## Current Status

The engine has a complete architecture with:
- Full bitboard move generation
- Alpha-beta search with transposition tables
- Basic evaluation function
- UCI protocol implementation
- Comprehensive Perft test suite

Active development focuses on search improvements and evaluation refinement.