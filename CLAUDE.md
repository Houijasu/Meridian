# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Meridian is a modern chess engine being developed in C# (.NET 9.0). The project aims to create a competitive chess
engine using state-of-the-art techniques including bitboards, magic bitboards, advanced search algorithms, and neural
network evaluation.

### Project Configuration

- Native AOT compilation enabled for improved startup time and reduced memory usage
- Invariant globalization for consistent culture-independent behavior
- C# preview language features enabled
- Nullable reference types enabled for better null safety
- **Use the newest C# features like pattern matching and switch expressions whenever possible**

### Chess Engine Architecture

The engine follows a modular, layered architecture:

1. **UCI Interface Layer**: Universal Chess Interface protocol communication
2. **Game Management Layer**: Game state, move history, and position tracking
3. **Search Engine Layer**: Alpha-beta search with advanced pruning techniques
4. **Move Generation Layer**: Legal move generation using bitboards
5. **Evaluation Layer**: Position scoring (hand-crafted or neural network based)
6. **Board Representation Layer**: 64-bit bitboard position state

## Implementation Status

### ✅ Completed Components

#### Phase 1: Foundation (COMPLETED)

1. **Bitboard Representation** (`Core/Bitboard.cs`)
    - 64-bit integers for piece positions
    - Bit manipulation operations (SetBit, ClearBit, PopCount, etc.)
    - Hardware intrinsics support via System.Numerics
    - Shift operations for pawn moves

2. **Move Generation** (`Core/MoveGeneration/`)
    - Magic bitboards for sliding pieces (`MagicBitboards.cs`)
    - Individual piece generators: `PawnMoves.cs`, `KnightMoves.cs`, `BishopMoves.cs`, `RookMoves.cs`, `QueenMoves.cs`,
      `KingMoves.cs`
    - Attack detection (`AttackDetection.cs`) for check/pin detection
    - Capture-only generation for quiescence search
    - Special moves: castling, en passant, promotions
    - **100% perft accuracy** verified against multiple test suites

3. **Position Representation** (`Core/Position.cs`)
    - Struct-based for performance
    - Bitboards for each piece type and color
    - Make/unmake move functionality
    - FEN parsing and generation (`Core/Fen.cs`)
    - Zobrist hashing preparation

4. **Move Encoding** (`Core/Move.cs`)
    - Compact 32-bit move format
    - Flags for special moves (captures, promotions, castling, etc.)
    - Algebraic notation conversion

5. **Testing Infrastructure** (`Core/Testing/`)
    - Perft implementation with divide functionality
    - EPD test suite parser supporting multiple formats
    - Comprehensive test positions

#### Phase 2: Basic Engine (COMPLETED)

1. **Material Evaluation** (`Core/Evaluation/`)
    - Piece values: Pawn=100, Knight=320, Bishop=330, Rook=500, Queen=900
    - Piece-square tables for positional bonuses
    - Separate king tables for middlegame/endgame
    - Insufficient material detection

2. **Alpha-Beta Search** (`Core/Search/`)
    - Negamax with alpha-beta pruning
    - Quiescence search for tactical stability
    - Iterative deepening framework
    - Mate distance pruning
    - Basic time management
    - Search info reporting (depth, score, nodes, nps, pv)
    - Reaches depth 6-8 in seconds

3. **UCI Protocol** (`Core/UCI/UciProtocol.cs`)
    - Full UCI compliance for GUI integration
    - Position setup from FEN or moves
    - Time control handling (sudden death, increment, moves to go)
    - Search control (depth, time, infinite analysis)
    - Option declarations for future features
    - Non-standard commands: perft, display

### 🚧 Next Implementation Priorities

#### Phase 3: Search Enhancements

1. **Transposition Table**
    - Zobrist hashing for position keys
    - Replacement schemes (always replace, depth-preferred)
    - Exact, alpha, and beta bound storage
    - Move ordering from TT
    - Significant search speedup expected

2. **Move Ordering**
    - TT move first
    - MVV-LVA for captures
    - Killer moves (2 per ply)
    - History heuristic
    - Countermove heuristic

3. **Search Improvements**
    - Null move pruning (R=2 or R=3)
    - Late move reductions (LMR)
    - Principal variation search (PVS)
    - Aspiration windows
    - Futility pruning

#### Phase 4: Evaluation Enhancements

1. **Pawn Structure**
    - Passed pawns
    - Isolated pawns
    - Doubled pawns
    - Pawn chains
    - Pawn storms

2. **King Safety**
    - Pawn shield
    - Open files near king
    - Attack patterns
    - Safe squares

3. **Mobility**
    - Piece mobility counting
    - Trapped pieces
    - Outposts

4. **Endgame Knowledge**
    - Tapered evaluation
    - Specific endgame patterns
    - Rule of the square
    - Opposition

#### Phase 5: Advanced Features

1. **Multi-threading**
    - Lazy SMP (multiple threads searching)
    - Thread pool management
    - Shared transposition table

2. **Opening Book**
    - Polyglot format support
    - Book learning
    - Book creation tools

3. **Endgame Tablebases**
    - Syzygy tablebase probing
    - DTZ optimization
    - Tablebase in search

4. **NNUE Evaluation**
    - Efficiently updatable neural network
    - Incremental updates
    - Training infrastructure

## Performance Benchmarks

Current performance (as of Phase 2 completion):

- **Perft(6)**: 119M nodes in ~20 seconds (6M nps)
- **Search Speed**: 10-20M nps typical
- **Depth**: 6-8 plies in middlegame within seconds
- **Strength**: Estimated 1800-2000 Elo

## Testing Commands

### Perft Testing

```bash
# Run perft from starting position
dotnet run -- perft.epd 6

# Run specific EPD test file
dotnet run -- perftsuite.epd 6
```

### UCI Testing

```bash
# Start in UCI mode
dotnet run -- uci

# Or use launcher scripts
./meridian_uci.sh  # Linux/macOS
meridian_uci.bat   # Windows
```

### Development Testing

```bash
# Run default test suite
dotnet run

# Run with specific position
echo "position fen <fen> go depth 10" | dotnet run -- uci
```

## Code Style Guidelines

- Use modern C# features (pattern matching, switch expressions, etc.)
- Prefer `readonly struct` for data types
- Use `Span<T>` and `stackalloc` for temporary allocations
- Aggressive inlining for hot paths
- XML documentation comments are fine, but NO inline comments (// comments within methods)
- Prefer performance over abstraction

## Common Development Commands

### Build Commands

```bash
# Build the project
dotnet build

# Build in Release mode
dotnet build -c Release

# Clean build artifacts
dotnet clean
```

### Run Commands

```bash
# Run the application
dotnet run

# Run in Release mode
dotnet run -c Release

# Run in UCI mode
dotnet run -- uci
```

### Publishing Commands

```bash
# Publish as Native AOT application
dotnet publish -c Release

# Publish to specific runtime
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r osx-x64
```