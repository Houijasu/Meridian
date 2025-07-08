# PowerShell script to copy Meridian project from WSL and build it
# This avoids the UNC path issues with MSBuild

param(
    [string]$TargetPath = "C:\Meridian-Build",
    [string]$SourcePath = "\\wsl.localhost\Ubuntu-22.04\home\ereny\Meridian"
)

Write-Host "Meridian Chess Engine - Copy and Build Script" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

# Check if .NET SDK is installed
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK not found. Please install .NET 9.0 SDK" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Check if source exists
if (!(Test-Path $SourcePath)) {
    Write-Host "✗ Source path not found: $SourcePath" -ForegroundColor Red
    Write-Host "Please ensure WSL is running and path is correct" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Source path found: $SourcePath" -ForegroundColor Green

# Create target directory
Write-Host "Creating target directory: $TargetPath" -ForegroundColor Yellow
if (Test-Path $TargetPath) {
    Write-Host "Target directory exists. Removing..." -ForegroundColor Yellow
    Remove-Item -Path $TargetPath -Recurse -Force
}

New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
Write-Host "✓ Target directory created" -ForegroundColor Green

# Copy files
Write-Host ""
Write-Host "Copying project files..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Cyan

try {
    # Use robocopy for better performance with large projects
    $robocopyArgs = @(
        $SourcePath,
        $TargetPath,
        "/E",           # Copy subdirectories including empty ones
        "/R:1",         # Retry once on failed copies
        "/W:1",         # Wait 1 second between retries
        "/NFL",         # No file list (reduce output)
        "/NDL",         # No directory list
        "/NC",          # No class
        "/NS",          # No size
        "/NP"           # No progress
    )

    $result = & robocopy @robocopyArgs

    # Robocopy exit codes: 0-7 are success, 8+ are errors
    if ($LASTEXITCODE -lt 8) {
        Write-Host "✓ Files copied successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Copy failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Copy failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Navigate to project directory
Set-Location -Path "$TargetPath\Meridian"

Write-Host ""
Write-Host "Building project..." -ForegroundColor Yellow

# Clean first
Write-Host "Cleaning previous builds..." -ForegroundColor Cyan
dotnet clean Meridian.sln --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Clean failed" -ForegroundColor Red
    exit 1
}

# Build
Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build Meridian.sln -c Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    Write-Host "Try running: dotnet build Meridian.sln -c Release" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Build successful!" -ForegroundColor Green

# Publish
Write-Host ""
Write-Host "Publishing executable..." -ForegroundColor Yellow

$publishPath = "..\publish\win-x64"
dotnet publish Meridian\Meridian\Meridian.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true --output $publishPath --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Publish failed" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Publish successful!" -ForegroundColor Green

# Test the executable
Write-Host ""
Write-Host "Testing executable..." -ForegroundColor Yellow

$exePath = Join-Path $publishPath "Meridian.exe"
if (Test-Path $exePath) {
    $fileSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host "✓ Executable created: $exePath ($fileSize MB)" -ForegroundColor Green

    # Quick UCI test
    Write-Host "Running quick UCI test..." -ForegroundColor Cyan
    try {
        $testInput = "uci`nquit`n"
        $testOutput = $testInput | & $exePath

        if ($testOutput -match "uciok") {
            Write-Host "✓ UCI test passed" -ForegroundColor Green
        } else {
            Write-Host "⚠ UCI test unclear - check manually" -ForegroundColor Yellow
        }

        if ($testOutput -match "NNUE disabled") {
            Write-Host "✓ NNUE correctly disabled" -ForegroundColor Green
        }

        if ($testOutput -match "Traditional evaluation active") {
            Write-Host "✓ Traditional evaluation active" -ForegroundColor Green
        }

    } catch {
        Write-Host "⚠ Could not run UCI test: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ Executable not found at expected location" -ForegroundColor Red
    exit 1
}

# Final instructions
Write-Host ""
Write-Host "SUCCESS! Build completed." -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green
Write-Host ""
Write-Host "Executable location:" -ForegroundColor Cyan
Write-Host "  $exePath" -ForegroundColor White
Write-Host ""
Write-Host "To test opening moves:" -ForegroundColor Cyan
Write-Host "  cd `"$publishPath`"" -ForegroundColor White
Write-Host "  echo 'uci' | .\Meridian.exe" -ForegroundColor White
Write-Host "  echo 'position startpos' | .\Meridian.exe" -ForegroundColor White
Write-Host "  echo 'go depth 10' | .\Meridian.exe" -ForegroundColor White
Write-Host ""
Write-Host "Expected improvements:" -ForegroundColor Cyan
Write-Host "  ✓ Better opening moves: e4, d4, Nf3, c4" -ForegroundColor White
Write-Host "  ✓ No more terrible moves: a3, h3, Nh3" -ForegroundColor White
Write-Host "  ✓ Varied evaluations (not all 0.00)" -ForegroundColor White
Write-Host "  ✓ Traditional evaluation with opening principles" -ForegroundColor White
Write-Host ""

# Create quick test script
$testScriptPath = Join-Path $publishPath "quick-test.bat"
@"
@echo off
echo Testing Meridian Chess Engine...
echo.
echo Testing UCI protocol:
echo uci | Meridian.exe
echo.
echo Testing starting position (depth 8):
(echo position startpos & echo go depth 8) | Meridian.exe
echo.
echo Test complete!
pause
"@ | Out-File -FilePath $testScriptPath -Encoding ASCII

Write-Host "Quick test script created: $testScriptPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Run the test script to verify the engine works correctly!" -ForegroundColor Green
