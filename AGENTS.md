## Meridian AGENTS.md

This file provides essential guidelines for AI agents working on the Meridian chess engine.

### Essential Commands

- **Build:** `dotnet build`
- **Run all tests:** `dotnet test`
- **Run Perft tests (mandatory for move generation changes):** `dotnet test --filter "Category=Perft"`
- **Run single test:** `dotnet test --filter "TestName"`
- **Run the engine:** `dotnet run --project Meridian.CLI`
- **Release build:** `dotnet publish -c Release -r win-x64 --self-contained`

### Architecture Overview

**Meridian** is a high-performance C# UCI-compliant chess engine using .NET 9.0 with bitboard representation.

**Project Structure:**
- `Meridian.Core/` - Core engine (Board, MoveGeneration, Search, Evaluation, UCI)
- `Meridian.Tests/` - Tests (Perft, UCI, Search validation)
- `Meridian.CLI/` - Entry point

**Key Components:**
- Bitboard-based representation (LERF: A1=0, H8=63)
- Magic bitboards for sliding pieces with hardware intrinsics
- Negamax search with alpha-beta, transposition table, null move pruning
- UCI protocol with async command processing

### Code Style & Conventions

- **Formatting:** Follow `.editorconfig`. Use 4 spaces for indentation.
- **Naming:** Private fields: `_camelCase`, Static fields: `s_camelCase`
- **Types:** Use `#nullable enable`, file-scoped namespaces, `readonly struct` for immutable data
- **Performance:** **NO LINQ in hot paths**. Use `stackalloc`, object pooling, make/unmake pattern
- **Error Handling:** Never throw exceptions to GUI; follow UCI protocol
- **Comments:** Do not add comments unless requested
- **Rules:** See `Meridian/RULES.md` for comprehensive C# chess engine conventions
