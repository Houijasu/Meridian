# Fritz GUI Installation Troubleshooting Guide

## Fixed Issues

1. **Output Buffering**: Added `Console.Out.Flush()` after all UCI responses
2. **Console Title**: Removed console title setting that could fail in piped environments
3. **Case Sensitivity**: Added support for both "uci" and "UCI" commands
4. **Input Handling**: Added robust EOF and error handling
5. **Standalone Executable**: Created single-file Windows executable

## Installation Steps

1. **Use the Standalone Executable**
   - Use the file: `publish/Meridian.exe` (70MB standalone executable)
   - Copy this file to Fritz's engines directory
   - Do NOT use the DLL or require .NET runtime

2. **Add Engine in Fritz**
   - Go to Engine → Create UCI Engine
   - Browse to `Meridian.exe`
   - Click OK

## Debugging Steps

If Fritz still doesn't recognize the engine:

### 1. Enable Debug Mode
Create a batch file `Meridian_Debug.bat` in the same directory as `Meridian.exe`:
```batch
@echo off
Meridian.exe --debug
```

Then try to install `Meridian_Debug.bat` as the engine in Fritz. This will create a log file `uci_debug_[timestamp].log` in the same directory.

### 2. Test UCI Manually
Open Command Prompt and run:
```
cd [path to Meridian.exe]
Meridian.exe
uci
```

You should see:
```
id name Meridian 1.0
id author Meridian Team
option name Hash type spin default 128 min 1 max 2048
option name Threads type spin default 1 min 1 max 1
option name Ponder type check default false
uciok
```

### 3. Check Fritz Requirements
Some versions of Fritz have specific requirements:
- Engine must respond to "uci" within 10 seconds
- Engine must not output anything before receiving "uci"
- Engine must handle Windows line endings (CRLF)

### 4. Alternative Installation Methods

#### Method A: Direct Path
Instead of copying to engines folder, try:
1. Engine → Create UCI Engine
2. Use full path: `C:\Full\Path\To\Meridian.exe`

#### Method B: Compatibility Mode
1. Right-click `Meridian.exe`
2. Properties → Compatibility
3. Try "Windows 7" or "Windows 8" compatibility mode

### 5. Common Fritz Issues

1. **Antivirus**: Some antivirus software blocks new executables
   - Add exception for Meridian.exe

2. **Permissions**: Fritz might need admin rights
   - Run Fritz as Administrator

3. **Path Spaces**: Avoid spaces in path
   - Use `C:\Engines\Meridian.exe` not `C:\Chess Engines\Meridian.exe`

## Test Commands

After installation, test with these UCI commands in Fritz's engine window:
```
uci
isready
position startpos
go movetime 1000
```

## If All Else Fails

1. Send the debug log file (`uci_debug_*.log`) for analysis
2. Try other GUIs (Arena, Cute Chess) to verify the engine works
3. Check Fritz's own log files for error messages

## Known Working Configuration

- Windows 10/11 64-bit
- .NET 9.0 runtime (included in standalone exe)
- Fritz 16 or newer