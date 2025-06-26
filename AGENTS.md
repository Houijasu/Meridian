## Meridian AGENTS.md

This file provides essential guidelines for AI agents working on the Meridian chess engine.

### Essential Commands

- **Build:** `dotnet build`
- **Run all tests:** `dotnet test`
- **Run Perft tests (mandatory for move generation changes):** `dotnet test --filter "Category=Perft"`
- **Run the engine:** `dotnet run --project Meridian.CLI`

### Code Style & Conventions

- **Formatting:** Follow `.editorconfig`. Use 4 spaces for indentation.
- **Naming:**
    - Private fields: `_camelCase`
    - Static fields: `s_camelCase`
- **Types:**
    - Use `#nullable enable`.
    - Use file-scoped namespaces.
    - Use `readonly struct` for immutable data structures.
- **Performance:**
    - **NO LINQ in hot paths (search/evaluation).**
    - Avoid memory allocations in search/evaluation; use `stackalloc` and object pooling.
    - Use make/unmake pattern for board state changes.
- **Error Handling:** Do not throw exceptions to the GUI; follow UCI protocol for error reporting.
- **Comments:** Do not add comments unless requested.
