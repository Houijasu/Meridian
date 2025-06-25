# ROADMAP.md - Meridian C# UCI Chess Engine Development Guidelines

# Project Context
This is a C# UCI-compliant chess engine project using .NET 8.0 with a focus on performance, correctness, and maintainability. The engine implements the Universal Chess Interface (UCI) protocol and uses bitboard representation for optimal performance.

# Technology Stack
- Language: C# 12.0 with .NET 9.0
- Architecture: Bitboard-based with magic bitboards for sliding piece move generation
- Search: Negamax with alpha-beta pruning and various enhancements
- Protocol: UCI (Universal Chess Interface) compliant
- Testing: MSTest for unit tests, Perft for move generation validation

# Bash Commands
- `dotnet build`: Build the project
- `dotnet test`: Run all unit tests
- `dotnet run --project Meridian.CLI`: Run the engine in UCI mode
- `dotnet test --filter "Category=Perft"`: Run Perft tests only
- `dotnet publish -c Release -r win-x64 --self-contained`: Create release build

# Code Style - General C# Conventions

## Naming Conventions
- **PascalCase** for classes, methods, properties, events, namespaces: `Meridian`, `GenerateMoves()`
- **camelCase** for parameters and local variables: `moveCount`, `isCapture`
- **Private fields**: Use `_camelCase` prefix: `_transpositionTable`
- **Static fields**: Use `s_camelCase` prefix: `s_pieceValues`
- **Constants**: PascalCase: `StartingPositionFen`
- **Interfaces**: Prefix with 'I': `ISearchEngine`, `IEvaluator`

## File Organization
```
src/
├── Meridian.Core/
│   ├── Board/
│   │   ├── Bitboard.cs
│   │   ├── Position.cs
│   │   └── Move.cs
│   ├── MoveGeneration/
│   │   ├── MoveGenerator.cs
│   │   └── MagicBitboards.cs
│   ├── Search/
│   │   ├── SearchEngine.cs
│   │   ├── TranspositionTable.cs
│   │   └── MoveOrdering.cs
│   ├── Evaluation/
│   │   └── Evaluator.cs
│   └── UCI/
│       ├── UciEngine.cs
│       └── UciCommandParser.cs
├── Meridian.Tests/
│   ├── Perft/
│   ├── UCI/
│   └── Search/
└── Meridian.CLI/
    └── Program.cs
```

## Modern C# Features
- **Always enable nullable reference types**: `#nullable enable`
- **Use file-scoped namespaces**: `namespace Meridian.Core;`
- **Prefer pattern matching**: Use switch expressions for move scoring
- **Use records for immutable data**: `public record SearchResult(Move BestMove, int Score);`
- **Apply `[SkipLocalsInit]`** on performance-critical methods

# Chess Engine Specific Conventions

## Bitboard Representation
- Use **Little-Endian Rank-File (LERF)** mapping: A1=0, H1=7, A8=56, H8=63
- Bitboard type should be a struct wrapping `ulong`
- Always use hardware intrinsics when available: `Popcnt`, `Tzcnt`, `Pext`

```csharp
public readonly struct Bitboard
{
    private readonly ulong _value;

    public static int PopCount(Bitboard bb) =>
        BitOperations.PopCount(bb._value);
}
```

## Move Representation
- Pack moves into 32-bit struct for memory efficiency
- Include: From (6 bits), To (6 bits), Flags (4 bits), Captured piece (4 bits)
- Use readonly struct with proper equality implementation

## Performance Patterns
- **Use stackalloc for temporary arrays**: `Span<Move> moves = stackalloc Move[218];`
- **Avoid allocations in hot paths**: No LINQ, no heap allocations in search
- **Implement make-unmake pattern**: Never copy board state during search
- **Use ref returns and in parameters** for large structs

## UCI Implementation Rules
- UCI communication must be on separate thread from search
- Always respond to `stop` command within 50ms
- Send `readyok` only when truly ready
- Include PV (principal variation) in info strings
- Never throw exceptions to GUI - handle all errors internally

# Search Algorithm Conventions

## Alpha-Beta Search
- Use **Negamax** formulation for consistency
- Implement **staged move generation** for efficiency
- Apply **null move pruning** with verification in endgames
- Use **late move reductions** (LMR) for quiet moves

## Move Ordering Priority
1. Hash move from transposition table
2. Good captures (MVV-LVA)
3. Killer moves (2 per ply)
4. Counter moves
5. History heuristic
6. Bad captures

## Transposition Table
- Use **Zobrist hashing** with 64-bit keys
- Implement **depth-preferred replacement**
- Store: Key, Score, Depth, Move, Node type, Age
- Size must be power of 2 for fast indexing

# Testing Requirements

## Perft Testing
- **Every move generation change** must pass full Perft suite
- Test positions must include: Starting position, Kiwipete, special positions
- Use divide command for debugging
- Expected Perft values to depth 6 minimum

## Unit Testing Patterns
```csharp
[TestMethod]
[DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 20)]
[DataRow("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq -", 48)]
public void TestMoveGeneration(string fen, int expectedMoves)
{
    var position = Position.FromFen(fen);
    var moves = _moveGenerator.GenerateMoves(position);
    Assert.AreEqual(expectedMoves, moves.Count);
}
```

## EPD Test Suites
- Run standard test suites: WAC, ECM, STS
- Track solving percentage over development
- Time limit: 10 seconds per position
- Log positions that fail for analysis

# Documentation Standards

## XML Documentation
```csharp
/// <summary>
/// Performs quiescence search to avoid horizon effect.
/// Only considers captures and promotions.
/// </summary>
/// <param name="alpha">Lower bound of search window</param>
/// <param name="beta">Upper bound of search window</param>
/// <returns>Static evaluation or tactical resolution score</returns>
/// <remarks>
/// Performance: ~30% of total nodes in typical middlegame positions
/// </remarks>
public int QuiescenceSearch(int alpha, int beta)
```

## Algorithm Documentation
- Include complexity analysis: Time and space
- Explain non-obvious optimizations
- Reference papers/sources for advanced techniques
- Provide examples for complex algorithms

# Memory Management

## Object Pooling
- Pool move arrays for recursive search
- Reuse evaluation scratch buffers
- Clear transposition table between games

## Stack vs Heap
- Prefer stack allocation for temporary data
- Use `ArrayPool<T>` for larger temporary arrays
- Avoid boxing of value types

# Multithreading Patterns

## Parallel Search
- Implement **Lazy SMP** for simplicity
- Each thread has own search stack
- Share transposition table (lock-free)
- Use `CancellationToken` for search termination

## Thread Safety
- Transposition table must be thread-safe
- Use `Interlocked` operations for statistics
- Avoid locks in performance-critical paths

# Build and Release

## Compiler Optimizations
- Enable tiered compilation
- Use Profile-Guided Optimization (PGO) for releases
- Target specific CPU architectures when possible
- Enable `<TrimMode>link</TrimMode>` for smaller binaries

## Benchmarking
- Track nodes per second (NPS) for each version
- Maintain suite of benchmark positions
- Compare against reference engines
- Use BenchmarkDotNet for micro-benchmarks

# Workflow Rules

## Development Process
1. **Always run Perft tests** before committing move generation changes
2. **Profile before optimizing** - use PerfView or similar
3. **Maintain backward compatibility** with UCI protocol
4. **Version tag** all releases with Elo estimates

## Code Review Checklist
- [ ] Perft tests pass
- [ ] No allocations in hot paths
- [ ] Proper error handling (no exceptions to GUI)
- [ ] XML documentation for public APIs
- [ ] Performance impact measured

## Git Workflow
- Branch naming: `feature/description`, `bugfix/description`
- Commit format: `type(scope): description` (e.g., `feat(search): add aspiration windows`)
- Always squash merge feature branches
- Tag releases with version and estimated Elo

# Performance Guidelines

## Critical Path Optimizations
- Move generation: < 1 microsecond per position
- Evaluation: < 500 nanoseconds per position
- Make/Unmake: < 50 nanoseconds per move
- Hash probe: < 20 nanoseconds per lookup

## Memory Footprint
- Transposition table: Configurable, default 128MB
- Move generator tables: < 1MB total
- Search stack: < 1KB per ply

# Common Pitfalls to Avoid

1. **Using LINQ in search** - Creates allocations
2. **Copying position during search** - Use make/unmake
3. **Not handling time pressure** - Must move before time runs out
4. **Ignoring UCI spec** - Some GUIs are strict about protocol
5. **Premature optimization** - Profile first, optimize later

# Dependencies

## Allowed NuGet Packages
- `BenchmarkDotNet` (dev only)
- `System.Runtime.Intrinsics` (included in .NET)
- No external chess libraries - implement everything

## Code Analysis
- Enable all code analysis rules
- Suppress with justification only
- Zero warnings policy

# Remember

- **Correctness first, performance second** - A fast but incorrect engine is useless
- **Test everything** - Chess engines have many edge cases
- **Profile regularly** - Performance regressions are easy to introduce
- **Document complex algorithms** - Future you will thank present you
- **Have fun** - Chess programming is a journey, not a destination