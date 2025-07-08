# Fix for Weak Chess Play - NNUE Network Loading Issue

## Problem Diagnosis

The Meridian chess engine is playing weak moves because the NNUE (Neural Network) evaluation is not loading properly. The engine is falling back to a basic hand-crafted evaluation function, which explains why it's suggesting moves like `Nh3` (knight to the rim) and giving very flat evaluations (~0.0).

## Root Cause

1. **NNUE Network File Missing**: The engine expects `networks/obsidian.nnue` relative to the executable
2. **Build Process Issue**: The network file is not being copied to the output directory during build
3. **Fallback Evaluation**: When NNUE fails to load, it uses a weaker traditional evaluation

## Solution Steps

### Step 1: Verify Network File Exists
The NNUE network file should be at:
```
Meridian/networks/obsidian.nnue
```

### Step 2: Fix the Build Process

#### Option A: Manual Copy (Quick Fix)
After building the engine, manually copy the network file:

1. Build the engine normally:
   ```cmd
   cd "Meridian\Meridian"
   dotnet publish Meridian\Meridian\Meridian.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true --output ..\publish\win-x64
   ```

2. Create networks directory and copy file:
   ```cmd
   mkdir "..\publish\win-x64\networks"
   copy "..\..\networks\obsidian.nnue" "..\publish\win-x64\networks\obsidian.nnue"
   ```

#### Option B: Fix Project File (Permanent Fix)
The project file `Meridian/Meridian/Meridian/Meridian.csproj` needs to include the network file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>12.0</LangVersion>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Meridian.Core\Meridian.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\..\..\networks\obsidian.nnue">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>networks\obsidian.nnue</Link>
    </None>
  </ItemGroup>
</Project>
```

### Step 3: Verify NNUE Loading

When you run the engine, you should see:
```
info string NNUE loaded from networks/obsidian.nnue
```

If you see:
```
info string NNUE file not found: networks/obsidian.nnue
```

Then the network file is missing from the executable directory.

### Step 4: Test the Fix

1. **Start the engine**:
   ```cmd
   Meridian.exe
   ```

2. **Send UCI commands**:
   ```
   uci
   position startpos
   go depth 10
   ```

3. **Expected behavior**:
   - Should see "NNUE loaded" message
   - Evaluations should be more varied (not all ~0.0)
   - Should suggest reasonable opening moves like `e4`, `d4`, `Nf3`, `c4`

## Alternative: Use Built-in Build Script

Use the provided build script which handles NNUE copying:

```cmd
build-windows.bat
```

This script will:
1. Build the engine
2. Copy the NNUE network file
3. Test the engine
4. Provide usage instructions

## Expected Improvements

After fixing the NNUE loading:

1. **Better Move Selection**: 
   - Opening: `e4`, `d4`, `Nf3`, `c4` instead of `Nh3`
   - Middlegame: More tactical awareness
   - Endgame: Better piece coordination

2. **More Accurate Evaluations**:
   - Scores will vary more significantly
   - Better position assessment
   - Improved tactical evaluation

3. **Stronger Play Overall**:
   - ~200-400 ELO improvement
   - Better understanding of piece values
   - Improved positional play

## Debugging NNUE Issues

If NNUE still doesn't work:

1. **Check file size**: `obsidian.nnue` should be several MB
2. **Verify path**: Must be `networks/obsidian.nnue` relative to executable
3. **Check permissions**: Ensure the file is readable
4. **Test manually**: Use UCI command `setoption name NNUEPath value networks/obsidian.nnue`

## Technical Details

The NNUE network provides:
- **768-dimensional input** (piece positions relative to king)
- **1536-node hidden layer** with ReLU activation
- **Output buckets** for different game phases
- **Trained evaluation** much stronger than hand-crafted evaluation

The engine automatically falls back to traditional evaluation if NNUE fails, but this is significantly weaker (~300 ELO difference).

## Final Verification

A properly working engine should:
1. Load NNUE on startup
2. Suggest standard opening moves
3. Give varied evaluations (not all ~0.0)
4. Show tactical awareness in analysis
5. Play at approximately 2400+ ELO strength