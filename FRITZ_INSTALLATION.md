# Fritz GUI Installation Guide for Meridian Chess Engine

## Method 1: Using Batch File (Recommended)

1. Build the engine:
   ```
   dotnet build -c Release
   ```

2. In Fritz:
   - Go to Engine → Create UCI Engine
   - Browse to `meridian_fritz.bat`
   - Click OK

## Method 2: Using Compiled Executable

1. On a Windows machine, compile the engine:
   ```
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   ```

2. In Fritz:
   - Go to Engine → Create UCI Engine
   - Browse to the published `Meridian.exe`
   - Click OK

## Troubleshooting

If Fritz still cannot detect the engine:

1. **Test the engine manually**:
   - Open Command Prompt
   - Navigate to the engine directory
   - Run `meridian_fritz.bat`
   - Type `uci` and press Enter
   - You should see:
     ```
     id name Meridian
     id author Houijasu
     option name Hash type spin default 128 min 1 max 16384
     uciok
     ```

2. **Alternative Fritz Installation**:
   - In Fritz, go to Engine → Create UCI Engine
   - Click "Browse" and select `meridian_fritz.bat`
   - If name/author don't appear, manually enter:
     - Name: Meridian
     - Author: Houijasu
   - Click OK

3. **Check Fritz logs**:
   - Look in Fritz installation folder for error logs
   - Common location: `C:\Program Files (x86)\ChessBase\Fritz XX\debug.txt`

4. **Use Arena or other GUI**:
   - If Fritz continues to have issues, try Arena Chess GUI
   - Arena is often more forgiving with UCI protocol

## Known Issues with Fritz

- Fritz is very strict about UCI protocol compliance
- No output is allowed before the "uci" command
- Fritz may have issues with .NET runtime detection
- Some Fritz versions require exact UCI formatting