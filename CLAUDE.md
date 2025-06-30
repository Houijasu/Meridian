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

# Run the engine
cd Meridian && dotnet run --project Meridian/Meridian

# Create optimized release build
cd Meridian && dotnet publish -c Release -r linux-x64 --self-contained
```

## Test Scripts

Several test scripts are available at the root level for engine validation:

```bash
./quick_test.sh          # Basic depth 10 test from starting position
./test_deep_search.sh    # Deep search test (depth 25) with complex position
./test_fix.sh           # Basic deep search test (depth 30)
./test_fixed.sh         # Deep search test (depth 30) with specific position
```

## Architecture Overview

The project follows a three-tier structure:

1. **Meridian.Core** - Core chess engine library
   - Bitboard-based board representation
   - Magic bitboard move generation
   - Alpha-beta search with transposition tables
   - UCI protocol implementation

2. **Meridian.Tests** - Comprehensive test suite
   - Perft tests for move generation validation
   - UCI protocol compliance tests
   - Search algorithm tests

3. **Meridian** - Executable entry point

## Critical Development Rules

1. **Before ANY move generation commits**: Run full Perft test suite
   ```bash
   cd Meridian && dotnet test --filter "Category=Perft"
   ```

2. **Performance requirements**: 
   - Zero allocations in hot paths (search/evaluation)
   - Use make/unmake pattern, never copy board state
   - Target > 1 million NPS on modern hardware

3. **Code standards**:
   - .NET 9.0 with C# 12.0
   - Nullable reference types enabled
   - Zero warnings policy
   - NO comments unless specifically requested

## Important Notes

- For detailed architecture, performance requirements, and implementation guidelines, refer to `Meridian/CLAUDE.md`
- The project uses unsafe code for performance-critical operations
- UCI protocol compliance is strict - some GUIs are unforgiving
- Recent commits show active development on search improvements and bug fixes