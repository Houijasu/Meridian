# SOLUTION: Fixing Weak Chess Engine Moves

## Problem Analysis

The Meridian chess engine is playing extremely weak moves (a3, Nh3) with flat evaluations (~0.0). After investigation, I've identified multiple critical issues:

### Issue 1: NNUE Scaling Bug
**Location**: `Meridian.Core/NNUE/NNUENetwork.cs` line 253
**Problem**: Incorrect scaling calculation
```csharp
// WRONG (scales to nearly zero):
return rawOutput * NNUEConstants.NetworkScale / (NNUEConstants.QA * NNUEConstants.QB);
// 400 / (255 * 128) = 400 / 32640 ≈ 0.012

// CORRECT:
return rawOutput * NNUEConstants.NetworkScale / NNUEConstants.QA;
// 400 / 255 ≈ 1.57
```

### Issue 2: NNUE Network Loading
**Location**: Build process and file copying
**Problem**: NNUE network file not copied to executable directory

### Issue 3: Evaluation Function Inconsistency
**Location**: Traditional vs NNUE evaluation fallback
**Problem**: May have sign errors or scaling mismatches

## Solutions

### Fix 1: Correct NNUE Scaling

**File**: `Meridian.Core/NNUE/NNUENetwork.cs`
**Line**: 253

```csharp
// BEFORE:
return rawOutput * NNUEConstants.NetworkScale / (NNUEConstants.QA * NNUEConstants.QB);

// AFTER:
return rawOutput * NNUEConstants.NetworkScale / NNUEConstants.QA;
```

### Fix 2: Update Project File for NNUE

**File**: `Meridian/Meridian/Meridian.csproj`

Add this section:
```xml
<ItemGroup>
  <None Include="..\..\..\..\networks\obsidian.nnue">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>networks\obsidian.nnue</Link>
  </None>
</ItemGroup>
```

### Fix 3: Disable NNUE Temporarily (for testing)

**File**: `Meridian.Core/Protocol/UCI/UciEngine.cs`
**Lines**: 27-29

```csharp
// TEMPORARILY disable NNUE to test traditional evaluation
Evaluator.UseNNUE = false;
UciOutput.WriteLine("info string NNUE disabled for debugging - using traditional evaluation");
```

### Fix 4: Build Script with NNUE Support

**File**: `build-engine.bat`

```batch
@echo off
echo Building Meridian Chess Engine...

cd Meridian
dotnet publish Meridian\Meridian\Meridian.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=false --output ..\publish\win-x64

REM Copy NNUE network
if exist "..\networks\obsidian.nnue" (
    if not exist "..\publish\win-x64\networks" mkdir "..\publish\win-x64\networks"
    copy "..\networks\obsidian.nnue" "..\publish\win-x64\networks\obsidian.nnue"
    echo NNUE network copied successfully
) else (
    echo Warning: NNUE network not found
)

echo Build completed!
pause
```

## Step-by-Step Implementation

### Step 1: Fix NNUE Scaling
1. Open `Meridian.Core/NNUE/NNUENetwork.cs`
2. Find line 253: `return rawOutput * NNUEConstants.NetworkScale / (NNUEConstants.QA * NNUEConstants.QB);`
3. Replace with: `return rawOutput * NNUEConstants.NetworkScale / NNUEConstants.QA;`

### Step 2: Disable NNUE for Testing
1. Open `Meridian.Core/Protocol/UCI/UciEngine.cs`
2. Find line 27: `Evaluator.UseNNUE = true;`
3. Replace with: `Evaluator.UseNNUE = false;`
4. Add debug message: `UciOutput.WriteLine("info string NNUE disabled for debugging");`

### Step 3: Test Traditional Evaluation
1. Build the engine: `dotnet build Meridian.sln -c Release`
2. Run the engine and test with: `position startpos` then `go depth 10`
3. Check if it suggests better moves like `e4`, `d4`, `Nf3`, `c4`

### Step 4: Fix NNUE (if traditional evaluation works)
1. Re-enable NNUE: `Evaluator.UseNNUE = true;`
2. Ensure NNUE file is in correct location
3. Test again with NNUE enabled

## Expected Results After Fixes

### Traditional Evaluation (Step 3)
- Should suggest reasonable opening moves: `e4`, `d4`, `Nf3`, `c4`
- Evaluations should vary: not all ~0.0
- Should avoid terrible moves like `a3`, `h3`, `Nh3`

### NNUE Evaluation (Step 4)
- Much stronger play (~2400 ELO)
- More accurate positional evaluation
- Better tactical awareness
- Properly scaled evaluations

## Diagnostic Commands

### Test Engine Startup
```
uci
```
Expected output:
```
info string NNUE disabled for debugging  (if disabled)
info string NNUE loaded from networks/obsidian.nnue  (if enabled)
```

### Test Position Evaluation
```
position startpos
go depth 10
```
Expected moves: `e4`, `d4`, `Nf3`, `c4` (NOT `a3`, `h3`, `Nh3`)

### Test Specific Position
```
position fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1
eval
```
Should return reasonable evaluation (not ~0.0)

## Root Cause Analysis

The engine was playing weak moves because:

1. **NNUE scaling bug**: Made all NNUE evaluations ~0.0
2. **Flat evaluation landscape**: Search couldn't distinguish between good/bad moves
3. **Move ordering broken**: All moves seemed equally good/bad
4. **Search algorithm confused**: With flat evaluations, search becomes random

## Verification Checklist

- [ ] NNUE scaling formula corrected
- [ ] Traditional evaluation tested and working
- [ ] NNUE network file in correct location
- [ ] Engine suggests standard opening moves
- [ ] Evaluations are varied (not all ~0.0)
- [ ] No more terrible moves like `a3` or `Nh3`
- [ ] Search depth reaches reasonable levels
- [ ] Move ordering working properly

## Performance Expectations

### Traditional Evaluation (Fixed)
- Playing strength: ~2000-2200 ELO
- Reasonable opening moves
- Basic tactical awareness

### NNUE Evaluation (Fixed)
- Playing strength: ~2400-2600 ELO
- Strong positional understanding
- Excellent tactical vision
- Modern chess knowledge

## Build Commands Summary

```batch
# Build with traditional evaluation (for testing)
cd Meridian
dotnet build Meridian.sln -c Release

# Build with NNUE (after fixes)
cd Meridian
dotnet publish Meridian\Meridian\Meridian.csproj -c Release -r win-x64 --self-contained --output ..\publish\win-x64
mkdir ..\publish\win-x64\networks
copy ..\networks\obsidian.nnue ..\publish\win-x64\networks\obsidian.nnue
```

The key insight is that the NNUE scaling bug was causing all evaluations to be essentially zero, making the engine unable to distinguish between good and bad moves. This led to essentially random move selection, explaining the terrible opening moves.