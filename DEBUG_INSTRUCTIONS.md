# Debugging the Fritz Move Issue

The engine now includes comprehensive logging to help debug why it gets stuck after making a move.

## How to Use the Logging

1. **Replace the engine in Fritz**:
   - Copy the new `publish/Meridian.exe` to your Fritz engines folder
   - Restart Fritz and select Meridian engine

2. **Check the log file**:
   - After reproducing the issue (engine gets stuck after a move), look for a file named:
     `meridian_uci_YYYYMMDD_HHMMSS.txt` in the same directory as Meridian.exe
   - This file contains all UCI communication and internal engine state

3. **What to look for in the log**:
   - `[IN]` - Commands received from Fritz
   - `[OUT]` - Responses sent to Fritz
   - `[INF]` - Information about internal processing
   - `[SCH]` - Search-related information
   - `[ERR]` - Any errors

## Common Issues and Solutions

### Issue 1: Engine doesn't receive "go" command
- Check if Fritz sends a "go" command after the position update
- Look for `[IN] go` in the log

### Issue 2: Position parsing fails
- Check for `[ERR] Failed to parse move` entries
- Verify the move format Fritz sends (should be like "e2e4")

### Issue 3: Search never completes
- Look for `[SCH] DoSearch started` without corresponding `[SCH] DoSearch completed`
- Check if `FindBestMove` returns or gets stuck

### Issue 4: Bestmove not sent
- Look for `[SCH] FindBestMove returned` and check if move data is 0
- Verify `[OUT] bestmove` is sent

## Test Sequence

To test manually, send these commands in order:
```
uci
isready
position startpos moves e2e4
go wtime 300000 btime 300000
```

The engine should respond with a bestmove within a few seconds.

## Send the Log

If the issue persists, please share the contents of the log file. The most important parts are:
1. The last few `[IN]` commands before the engine gets stuck
2. Any `[ERR]` entries
3. The last few `[SCH]` entries showing search status