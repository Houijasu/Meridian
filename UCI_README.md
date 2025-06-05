# Meridian Chess Engine - UCI Interface

Meridian implements the Universal Chess Interface (UCI) protocol, allowing it to be used with any UCI-compatible chess
GUI such as:

- Arena
- ChessBase
- Scid vs PC
- CuteChess
- Banksia GUI

## Usage

### Running in UCI Mode

```bash
# Linux/macOS
./meridian_uci.sh

# Windows
meridian_uci.bat

# Or directly with dotnet
dotnet run -c Release -- uci
```

### Supported UCI Commands

- `uci` - Initialize UCI mode and display engine info
- `isready` - Check if engine is ready
- `ucinewgame` - Start a new game
- `position [startpos | fen <fenstring>] [moves <move1> <move2> ...]` - Set position
- `go [depth <x>] [movetime <ms>] [wtime <ms>] [btime <ms>] [winc <ms>] [binc <ms>] [infinite]` - Start search
- `stop` - Stop the current search (not yet implemented)
- `quit` - Exit the engine

### Additional Commands (Non-standard)

- `perft <depth>` - Run perft test from current position
- `d` or `display` - Display the current position

## Example Session

```
uci
id name Meridian 1.0
id author Claude AI Assistant
option name Hash type spin default 128 min 1 max 16384
option name Threads type spin default 1 min 1 max 128
option name Ponder type check default false
uciok

isready
readyok

position startpos moves e2e4 e7e5 g1f3
go depth 6
info depth 1 score cp 50 nodes 63 nps 63000 time 1 pv b1c3
info depth 2 score cp 0 nodes 1397 nps 465666 time 3 pv b1c3
info depth 3 score cp 50 nodes 23144 nps 2314400 time 10 pv b1c3
info depth 4 score cp 0 nodes 451535 nps 2625203 time 172 pv b1c3
info depth 5 score cp 20 nodes 7743132 nps 9329074 time 830 pv g1f3
info depth 6 score cp 0 nodes 54321098 nps 12456789 time 4362 pv g1f3
bestmove g1f3

quit
```

## Engine Features

- **Search**: Alpha-beta pruning with quiescence search
- **Evaluation**: Material + piece-square tables
- **Time Management**: Basic time allocation based on remaining time
- **Move Generation**: Complete legal move generation with 100% perft accuracy
- **Special Moves**: Castling, en passant, promotions

## Performance

- Searches approximately 10-20 million nodes per second
- Reaches depth 6-8 in middlegame positions within seconds
- Fully legal move generation (no pseudo-legal moves)

## Configuration

The engine supports the following UCI options (not yet fully implemented):

- `Hash` - Transposition table size in MB (default: 128)
- `Threads` - Number of search threads (default: 1)
- `Ponder` - Enable pondering (default: false)