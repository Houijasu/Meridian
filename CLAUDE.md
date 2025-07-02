# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Meridian is a high-performance UCI-compliant chess engine written in C# (.NET 9.0). The main development happens in the `Meridian/` subdirectory which contains its own comprehensive CLAUDE.md file with detailed architecture and development guidelines.

## Essential Commands

```bash
# Build the entire solution
cd Meridian && dotnet build Meridian.sln

# Run all tests
cd Meridian && dotnet test Meridian.sln

# Run specific test categories
cd Meridian && dotnet test --filter "Category=Perft"  # Critical for move generation changes
cd Meridian && dotnet test --filter "Category=Search"  # Search algorithm tests
cd Meridian && dotnet test --filter "Category=UCI"     # UCI protocol compliance

# Run specific test classes
cd Meridian && dotnet test --filter "FullyQualifiedName~Meridian.Tests.Perft"

# Run the engine
cd Meridian && dotnet run --project Meridian/Meridian

# Run the engine with UCI commands
echo -e "uci\nposition startpos\ngo depth 10\nquit" | dotnet run --project Meridian/Meridian

# Create optimized release build
cd Meridian && dotnet publish -c Release -r linux-x64 --self-contained

# Build with specific runtime identifiers
cd Meridian && dotnet publish -c Release -r win-x64 --self-contained
cd Meridian && dotnet publish -c Release -r osx-x64 --self-contained
```

## Architecture Overview

The project follows a three-tier structure:

1. **Meridian.Core** - Core chess engine library
   - Bitboard-based board representation with hardware intrinsics
   - Magic bitboard move generation for sliding pieces
   - Alpha-beta search with transposition tables
   - UCI protocol implementation
   - Zero allocation design in performance-critical paths

2. **Meridian.Tests** - Comprehensive test suite
   - 60+ Perft tests for move generation validation
   - UCI protocol compliance tests
   - Search algorithm and evaluation tests
   - Uses both xUnit and MSTest frameworks

3. **Meridian** - Executable entry point
   - Minimal wrapper around UCI engine
   - Console application for chess GUI integration

## Critical Development Rules

1. **Before ANY move generation commits**: Run full Perft test suite
   ```bash
   cd Meridian && dotnet test --filter "Category=Perft"
   ```

2. **Performance requirements**: 
   - Zero allocations in hot paths (search/evaluation)
   - Use make/unmake pattern, never copy board state
   - Target > 1 million NPS on modern hardware
   - Hardware intrinsics enabled (Popcnt, Tzcnt, Pext)

3. **Code standards**:
   - .NET 9.0 with C# 12.0
   - Nullable reference types enabled
   - Zero warnings policy (warnings as errors)
   - NO comments unless specifically requested
   - Private fields prefixed with underscore

## Testing Strategy

The project uses extensive Perft (performance test) validation for move generation correctness. Key test areas:

- **Perft Tests**: Validates move generation against known positions with exact node counts
- **Search Tests**: Validates search algorithm behavior and move ordering
- **UCI Tests**: Ensures strict UCI protocol compliance for GUI compatibility
- **Board Tests**: Validates board manipulation and position setup

## Important Notes

- For detailed architecture, performance requirements, and implementation guidelines, refer to `Meridian/CLAUDE.md`
- The project uses unsafe code for performance-critical operations
- UCI protocol compliance is strict - some GUIs are unforgiving
- Recent commits show active development on search improvements and bug fixes
- Code analysis suppressions in .editorconfig are performance-focused (e.g., CA1822 for static members)