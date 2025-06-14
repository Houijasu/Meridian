# Meridian Chess Engine

A high-performance chess engine written in C# with zero-allocation design principles.

## Usage

### UCI Mode (for Chess GUIs)
```bash
Meridian.exe uci
```
Compatible with any UCI-compliant chess GUI such as:
- Arena
- Cutechess
- Scid vs PC
- ChessBase
- Lichess Bot

### Console Mode
```bash
Meridian.exe
```
Interactive console with commands for testing and playing.

## Features

### Core Engine
- **Bitboard-based board representation** for efficient move generation
- **Magic bitboards** for sliding piece attacks (rooks, bishops, queens)
- **Complete move generation** including all special moves (castling, en passant, promotions)
- **Perft testing suite** with 100% accuracy on all standard positions
- **Zero-allocation design** using ref structs and Span<T>
- **Modern C# 13 preview features** for maximum performance

### Search Algorithm
- **Alpha-beta search** with iterative deepening
- **Transposition table** with Zobrist hashing (128MB default)
- **Quiescence search** for tactical stability
- **Null move pruning** (R=2/3 adaptive reduction)
- **Late Move Reductions (LMR)** for efficient deep searches
- **Move ordering framework** with killer moves and history heuristic

### Evaluation
- **Material and positional evaluation** with piece-square tables
- **Bishop pair bonus**
- **Separate king position tables** for middlegame and endgame

### Interface
- **UCI Protocol support** for chess GUI compatibility
- **Interactive console interface** for playing chess
- **FEN support** for position setup
- **Extensible protocol system** via IProtocol interface