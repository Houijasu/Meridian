# Meridian Chess Engine

A high-performance chess engine written in C# with zero-allocation design principles.

## Features

- **Bitboard-based board representation** for efficient move generation
- **Magic bitboards** for sliding piece attacks (rooks, bishops, queens)
- **Complete move generation** including all special moves (castling, en passant, promotions)
- **Perft testing suite** with 100% accuracy on all standard positions
- **Alpha-beta search** with iterative deepening
- **Transposition table** with Zobrist hashing (128MB default)
- **Quiescence search** for tactical stability
- **Material and positional evaluation** with piece-square tables
- **Interactive console interface** for playing chess
- **Zero-allocation design** using ref structs and Span<T>
- **Modern C# 13 preview features** for maximum performance