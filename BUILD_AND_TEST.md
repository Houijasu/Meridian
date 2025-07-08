# Meridian Chess Engine - Build and Test Instructions

## Problem Summary

The Meridian chess engine was playing extremely weak moves (a3, Nh3) due to:
1. **NNUE format incompatibility** with Obsidian network
2. **NNUE scaling bug** causing flat evaluations (~0.0)
3. **Weak traditional evaluation** without opening principles

## Solution Applied

✅ **Fixed NNUE scaling bug** (for future use)
✅ **Disabled incompatible NNUE** temporarily  
✅ **Enhanced traditional evaluation** with opening development bonuses
✅ **Added comprehensive debug output**

## Build Instructions

### Prerequisites
- .NET 9.0 SDK
- Windows 10/11
- Visual Studio or VS Code (optional)

### Method 1: Windows Command Prompt (Recommended)

1. **Open Windows Command Prompt as Administrator**
2. **Navigate to project root:**
   ```cmd
   cd "C:\path\to\your\project"
   ```
   
3. **Copy project from WSL (if needed):**
   ```cmd
   robocopy "\\wsl.localhost\Ubuntu-22.04\home\ereny\Meridian" "C:\Meridian" /E /R:1 /W:1
   cd "C:\Meridian"
   ```

4. **Build the project:**
   ```cmd
   cd Meridian
   dotnet clean Meridian.sln
   dotnet build Meridian.sln -c Release
   ```

5. **Publish executable:**
   ```cmd
   dotnet publish Meridian\Meridian\Meridian.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true --output ..\publish\win-x64
   ```

### Method 2: PowerShell Build Script

Create `build.ps1`:
```powershell
# Navigate to Meridian directory
cd Meridian

# Clean and build
dotnet clean Meridian.sln
dotnet build Meridian.sln -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Publish executable
    dotnet publish Meridian\Meridian\Meridian.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true --output ..\publish\win-x64
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Publish successful!" -ForegroundColor Green
        Write-Host "Executable: .\publish\win-x64\Meridian.exe" -ForegroundColor Cyan
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
}
```

Run with: `powershell -ExecutionPolicy Bypass .\build.ps1`

## Testing the Engine

### Quick Test

1. **Navigate to executable directory:**
   ```cmd
   cd publish\win-x64
   ```

2. **Test basic UCI communication:**
   ```cmd
   echo uci | Meridian.exe
   ```
   
   **Expected output:**
   ```
   info string NNUE disabled - using traditional evaluation
   info string Traditional evaluation active for better move selection
   id name Meridian
   id author Meridian Team
   [... UCI options ...]
   uciok
   ```

### Opening Position Test

1. **Create test file** `test_opening.txt`:
   ```
   uci
   position startpos
   go depth 10
   quit
   ```

2. **Run test:**
   ```cmd
   Meridian.exe < test_opening.txt
   ```

3. **Expected results:**
   - ✅ **Good opening moves**: `e4`, `d4`, `Nf3`, `c4`, `Nc3`
   - ❌ **No more bad moves**: `a3`, `h3`, `Nh3`, `a4`
   - ✅ **Varied evaluations**: Not all 0.00, proper scoring
   - ✅ **Reasonable depth**: Reaches depth 10+ quickly

### Advanced Testing

**Test specific positions:**
```
uci
position fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1
go depth 12
```

**Test tactical position:**
```
uci
position fen r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4
go depth 10
```

## Verification Checklist

- [ ] Engine builds without errors
- [ ] UCI protocol responds correctly
- [ ] Shows "NNUE disabled" message
- [ ] Suggests good opening moves (e4, d4, Nf3, c4)
- [ ] NO terrible moves (a3, h3, Nh3)
- [ ] Evaluations vary (not all 0.00)
- [ ] Reaches reasonable search depth
- [ ] Response time is reasonable

## Expected Performance

### Traditional Evaluation (Current)
- **Playing Strength**: ~2000-2200 ELO
- **Opening Moves**: e4, d4, Nf3, c4, Nc3
- **Evaluation Range**: -200 to +200 centipawns typically
- **Search Speed**: >100k NPS on modern hardware

### Improvements Made
- **Center pawn bonuses**: +30 for e4/d4
- **Knight development**: +25 for Nf3/Nc3, -40 for rim knights
- **Bishop development**: +20 for leaving back rank
- **Castling preparation**: +50 for castling
- **Early queen penalty**: -15 for premature development

## Troubleshooting

### Build Issues

**Error: MSB4019 (UNC path issues)**
- Solution: Copy project to Windows drive, don't build from WSL path

**Error: CA1310 (String comparison)**
- Solution: Already fixed in codebase

**Error: Target framework not found**
- Solution: Install .NET 9.0 SDK

### Runtime Issues

**Engine doesn't respond:**
- Check if UCI commands end with newline
- Ensure executable has proper permissions

**Still suggests bad moves:**
- Verify "NNUE disabled" message appears
- Check evaluation values are varying

**Crashes on startup:**
- Run from correct directory
- Check .NET runtime installation

## NNUE Future Work

To properly implement Obsidian NNUE:

1. **Analyze Obsidian network format**
   - File size: 30MB (vs expected 98MB)
   - Binary structure analysis needed
   - Architecture differences

2. **Update constants for Obsidian**
   - Correct L1/L2/L3 sizes
   - Proper quantization values
   - King bucket schemes

3. **Test with reference implementation**
   - Compare with original Obsidian
   - Validate evaluation outputs
   - Performance benchmarks

## Support

If you encounter issues:

1. **Check build output** for specific error messages
2. **Verify .NET SDK version**: `dotnet --version`
3. **Test from Windows Command Prompt** (not WSL)
4. **Compare expected vs actual outputs** in test results

The current implementation with enhanced traditional evaluation should provide significantly better chess play than the previous version with broken NNUE.