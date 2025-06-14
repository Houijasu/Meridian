# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a minimal C# console application using .NET 10.0 (Preview) with modern C# language features enabled.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run --project Meridian/Meridian.csproj

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore
```

## Project Structure

- `Meridian.slnx` - Solution file
- `Meridian/` - Main project directory
  - `Meridian.csproj` - Project configuration (targets .NET 10.0, enables preview features)
  - `Program.cs` - Entry point with ModuleInitializer that sets console title

## Key Configuration

The project uses:
- Target Framework: net10.0
- C# Language Version: Preview
- Nullable Reference Types: Enabled
- Implicit Usings: Enabled
- Unsafe Code: Allowed

## Development Notes

- No test framework is currently configured
- No linting tools beyond default .NET analyzers
- Core bitboard engine implemented with move generation
- Perft tests validate move generation correctness

## Performance Guidelines

- Zero-allocation principle: All hot paths must avoid heap allocations
- Use `ref struct` and `Span<T>` for temporary data
- Prefer stack allocation and object pooling
- Use latest C# features (C# 13 preview)
- Leverage SIMD intrinsics where applicable

## Architectural Constraints

- Project must obey 0-allocation principle.

## Code Guidelines

- Latest C# features must be used.
- Use Span<> and ref struct wherever possible.