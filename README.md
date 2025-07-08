# Meridian Chess Engine

A high-performance UCI-compliant chess engine written in C# (.NET 9.0) with NNUE support.

## Features

- **UCI Protocol Compliant**: Full compatibility with popular chess GUIs (Arena, Cute Chess, BanksiaGUI, etc.)
- **Advanced Search Algorithm**: 
  - Multi-threaded parallel alpha-beta search with pruning
  - Transposition tables for position caching
  - Move ordering optimizations
  - Aspiration windows and iterative deepening
- **NNUE Evaluation**: Neural network evaluation using the Obsidian network for superior positional understanding
- **Bitboard Architecture**: 
  - Hardware-accelerated move generation using intrinsics (Popcnt, Tzcnt, Pext)
  - Magic bitboards for sliding piece moves
  - Zero-allocation design in performance-critical paths
- **Performance**: Targets >1 million nodes per second on modern hardware
- **Comprehensive Testing**: 60+ Perft tests ensuring move generation correctness

## Requirements

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Any UCI-compatible chess GUI for playing

## Quick Start

### Building from Source

```bash
# Clone the repository
git clone https://github.com/Houijasu/Meridian.git
cd Meridian

# Build the engine
cd Meridian
dotnet build Meridian.sln --configuration Release

# Run the engine
dotnet run --project Meridian/Meridian
```

### Creating a Standalone Executable

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained

# macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

The standalone executable will be in the `publish` directory.

## Usage

### With a Chess GUI

1. Open your chess GUI (Arena, Cute Chess, etc.)
2. Add a new engine and select the Meridian executable
3. Configure time controls and analysis settings as desired

### Direct UCI Commands

```bash
# Start the engine and test basic functionality
echo -e "uci\nposition startpos\ngo depth 10\nquit" | dotnet run --project Meridian/Meridian
```

Common UCI commands:
- `uci` - Initialize the engine
- `position startpos moves e2e4 e7e5` - Set up a position
- `go depth 10` - Search to depth 10
- `go movetime 5000` - Search for 5 seconds
- `quit` - Exit the engine

## Development

### Project Structure

- **Meridian.Core/** - Core engine library (board representation, search, evaluation)
- **Meridian/** - UCI protocol implementation and executable entry point
- **Meridian.Tests/** - Comprehensive test suite including Perft validation

### Running Tests

```bash
# Run all tests
dotnet test Meridian.sln

# Run specific test categories
dotnet test --filter "Category=Perft"   # Move generation tests
dotnet test --filter "Category=Search"  # Search algorithm tests
dotnet test --filter "Category=UCI"     # UCI protocol tests
```

### Performance Testing

The engine includes extensive Perft tests for validating move generation:

```bash
# Run Perft benchmarks
dotnet test --filter "FullyQualifiedName~Meridian.Tests.Perft"
```

## Contributing

Contributions are welcome! Please ensure:

1. All Perft tests pass before submitting move generation changes
2. Code follows C# conventions with nullable reference types enabled
3. Zero allocations in performance-critical paths
4. No compiler warnings (warnings are treated as errors)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Copyright (c) 2025 Houijasu

## Acknowledgments

- UCI protocol specification by Stefan Meyer-Kahlen
- Obsidian NNUE network for neural network evaluation
- Chess programming wiki and community for algorithmic insights