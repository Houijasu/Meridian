# Meridian Chess Engine

A modern chess engine written in C# (.NET 9.0) with a focus on performance and code clarity.

## Features

### Core Engine
- **64-bit Bitboards** - Efficient board representation with hardware intrinsics support
- **Magic Bitboards** - Fast sliding piece move generation
- **100% Perft Accuracy** - Thoroughly tested move generation
- **FEN Support** - Standard position notation parsing

### Search Algorithm
- **Alpha-Beta Pruning** with quiescence search
- **Transposition Table** (128MB default, configurable)
- **Advanced Move Ordering**:
  - MVV-LVA (Most Valuable Victim - Least Valuable Attacker)
  - Killer moves (2 per ply)
  - History heuristic
  - Countermove heuristic
- **Search Enhancements**:
  - Null move pruning (R=3) with zugzwang detection
  - Late Move Reductions (LMR)
  - Principal Variation Search (PVS)
  - Aspiration windows
  - Futility pruning

### Evaluation
- Material evaluation with standard piece values
- Piece-square tables for positional assessment
- Separate king position tables for middlegame/endgame
- Insufficient material detection

### Performance
- **Native AOT Compilation** support for faster startup
- **Zero-allocation hot paths** for maximum performance
- **10-20M nodes/second** typical search speed
- Reaches **depth 20+** in seconds on modern hardware

## Building

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Any OS (Windows, Linux, macOS)

### Build Commands
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Native AOT publish (Windows x64)
dotnet publish -c Release -r win-x64
```

## Usage

### UCI Mode (for Chess GUIs)
```bash
# Start in UCI mode
dotnet run -- uci

# Or use the launcher scripts
./meridian_uci.sh   # Linux/macOS
meridian_uci.bat    # Windows
```

### Perft Testing
```bash
# Run perft test suite
dotnet run -- perft.epd 6

# Test specific position
dotnet run
perft 6
```

### Compatible GUIs
- **Arena Chess GUI** - Recommended, free
- **Cutechess** - Excellent for testing
- **Fritz** - See FRITZ_INSTALLATION.md for setup
- **ChessBase** - Full compatibility
- **Banksia GUI** - Modern and fast
- Any other UCI-compatible GUI

## Project Structure
```
Meridian/
├── Core/
│   ├── Bitboard.cs          # Bitboard operations
│   ├── Move.cs              # Move representation
│   ├── Position.cs          # Board position state
│   ├── Evaluation/          # Position evaluation
│   ├── MoveGeneration/      # Legal move generation
│   ├── Search/              # Search algorithms
│   └── UCI/                 # UCI protocol implementation
├── CLAUDE.md                # AI assistant instructions
└── Program.cs               # Entry point
```

## Development Status

### ✅ Completed (Phase 1-3)
- Bitboard representation
- Complete move generation
- Basic evaluation
- Alpha-beta search with enhancements
- Transposition table
- Advanced move ordering
- All major search improvements

### 🚧 Planned Features (Phase 4-5)
- Enhanced evaluation (pawn structure, king safety, mobility)
- Opening book support
- Endgame tablebases
- Multi-threading (Lazy SMP)
- NNUE evaluation

## Testing

### Perft Results
Position | Depth 6 | Time (ms)
---------|---------|----------
Starting | 119,060,324 | ~20,000
Kiwipete | 193,690,690 | ~35,000

### Strength
- Estimated **1800-2000 Elo** (current version)
- Solves most tactical positions quickly
- Competitive with other amateur engines

## Contributing
Contributions are welcome! Please ensure:
- Code follows existing style (modern C#, no inline comments)
- All tests pass
- Perft accuracy is maintained

## License
This project is open source. See LICENSE file for details.

## Acknowledgments
- Built with assistance from Claude AI
- Inspired by Stockfish, Crafty, and other great engines
- Thanks to the chess programming community

---
*Meridian - Where every move counts*