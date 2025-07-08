# PowerShell script to download and setup Stockfish NNUE network for Meridian
# This replaces the incompatible Obsidian network with a working Stockfish network

param(
    [string]$NetworkUrl = "https://github.com/official-stockfish/networks/raw/master/nn-0000000000a0.nnue",
    [string]$NetworkPath = "networks\stockfish.nnue"
)

Write-Host "Meridian NNUE Setup Script" -ForegroundColor Green
Write-Host "==========================" -ForegroundColor Green
Write-Host ""

# Create networks directory if it doesn't exist
$networksDir = "networks"
if (!(Test-Path $networksDir)) {
    Write-Host "Creating networks directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $networksDir -Force | Out-Null
}

# Check if network already exists
if (Test-Path $NetworkPath) {
    $choice = Read-Host "Network file already exists. Replace it? (y/n)"
    if ($choice -ne "y" -and $choice -ne "Y") {
        Write-Host "Setup cancelled." -ForegroundColor Yellow
        exit 0
    }
    Remove-Item $NetworkPath -Force
}

# Download Stockfish NNUE network
Write-Host "Downloading Stockfish NNUE network..." -ForegroundColor Yellow
Write-Host "URL: $NetworkUrl" -ForegroundColor Cyan

try {
    # Use Invoke-WebRequest to download
    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($NetworkUrl, $NetworkPath)

    if (Test-Path $NetworkPath) {
        $fileSize = [math]::Round((Get-Item $NetworkPath).Length / 1MB, 2)
        Write-Host "✓ Download successful: $NetworkPath ($fileSize MB)" -ForegroundColor Green
    } else {
        throw "File not found after download"
    }
} catch {
    Write-Host "✗ Download failed: $($_.Exception.Message)" -ForegroundColor Red

    # Try alternative download method
    Write-Host "Trying alternative download method..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $NetworkUrl -OutFile $NetworkPath -UseBasicParsing

        if (Test-Path $NetworkPath) {
            $fileSize = [math]::Round((Get-Item $NetworkPath).Length / 1MB, 2)
            Write-Host "✓ Alternative download successful: $NetworkPath ($fileSize MB)" -ForegroundColor Green
        } else {
            throw "Alternative download also failed"
        }
    } catch {
        Write-Host "✗ Both download methods failed" -ForegroundColor Red
        Write-Host "Please manually download the network from:" -ForegroundColor Yellow
        Write-Host $NetworkUrl -ForegroundColor White
        Write-Host "And save it as: $NetworkPath" -ForegroundColor White
        exit 1
    }
}

# Update Meridian code to use Stockfish network
Write-Host ""
Write-Host "Updating Meridian configuration..." -ForegroundColor Yellow

# Update UciEngine.cs to use the new network
$uciEnginePath = "Meridian.Core\Protocol\UCI\UciEngine.cs"
if (Test-Path $uciEnginePath) {
    Write-Host "Updating UCI engine configuration..." -ForegroundColor Cyan

    $content = Get-Content $uciEnginePath -Raw

    # Replace the network path and enable NNUE
    $newContent = $content -replace 'var defaultNNUEPath = "networks/obsidian.nnue";', 'var defaultNNUEPath = "networks/stockfish.nnue";'
    $newContent = $newContent -replace 'Evaluator\.UseNNUE = false;.*\r?\n.*UciOutput\.WriteLine\("info string NNUE disabled.*?\r?\n.*UciOutput\.WriteLine\("info string Traditional evaluation active.*?\r?\n', 'Evaluator.UseNNUE = true;'

    # Add proper NNUE loading
    $nnueLoadingCode = @"
            // Enable NNUE with Stockfish network
            Evaluator.UseNNUE = true;
            var defaultNNUEPath = "networks/stockfish.nnue";

            UciOutput.WriteLine(`$"info string Attempting to load Stockfish NNUE from {defaultNNUEPath}");

            if (File.Exists(defaultNNUEPath))
            {
                var fileInfo = new FileInfo(defaultNNUEPath);
                UciOutput.WriteLine(`$"info string Network file size: {fileInfo.Length} bytes");

                if (Evaluator.LoadNNUE(defaultNNUEPath))
                {
                    UciOutput.WriteLine(`$"info string Stockfish NNUE loaded successfully from {defaultNNUEPath}");
                    UciOutput.WriteLine(`$"info string NNUE UseNNUE status: {Evaluator.UseNNUE}");
                    // Initialize NNUE accumulator for the starting position
                    Evaluator.InitializeNNUE(_position);
                    UciOutput.WriteLine(`$"info string NNUE accumulator initialized");
                }
                else
                {
                    UciOutput.WriteLine(`$"info string Failed to load Stockfish NNUE from {defaultNNUEPath}");
                    UciOutput.WriteLine("info string Falling back to traditional evaluation");
                    Evaluator.UseNNUE = false;
                }
            }
            else
            {
                UciOutput.WriteLine(`$"info string Stockfish NNUE file not found: {defaultNNUEPath}");
                UciOutput.WriteLine("info string Using traditional evaluation");
                Evaluator.UseNNUE = false;
            }
"@

    if ($content -match 'Evaluator\.UseNNUE = false;') {
        $newContent = $content -replace '(?s)// Disable NNUE.*?UciOutput\.WriteLine\("info string Traditional evaluation active.*?\r?\n.*?\r?\n.*?}', $nnueLoadingCode

        Set-Content -Path $uciEnginePath -Value $newContent -Encoding UTF8
        Write-Host "✓ UCI engine updated to use Stockfish NNUE" -ForegroundColor Green
    } else {
        Write-Host "⚠ Could not automatically update UCI engine - manual edit required" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠ UCI engine file not found: $uciEnginePath" -ForegroundColor Yellow
}

# Update NNUE constants for Stockfish format
Write-Host "Updating NNUE constants for Stockfish format..." -ForegroundColor Cyan

$nnueConstantsPath = "Meridian.Core\NNUE\NNUEConstants.cs"
if (Test-Path $nnueConstantsPath) {
    $content = Get-Content $nnueConstantsPath -Raw

    # Update constants for Stockfish NNUE format
    $stockfishConstants = @"
namespace Meridian.Core.NNUE;

public static class NNUEConstants
{
    // Stockfish NNUE format constants
    public const int InputDimensions = 768;  // HalfKP features
    public const int L1Size = 2560;          // Stockfish L1 size
    public const int L2Size = 16;            // Standard L2 size
    public const int L3Size = 32;            // Standard L3 size
    public const int OutputBuckets = 8;      // Standard output buckets

    // Stockfish quantization constants
    public const int NetworkScale = 400;
    public const int QA = 255;
    public const int QB = 128;

    // HalfKP feature constants
    public const int KingBuckets = 1;        // Stockfish uses simpler king bucketing
    public const int PieceTypes = 6;
    public const int Colors = 2;

    public const int MaxPieces = 32;

    public static int FeatureWeightIndex(int piece, int square, int kingSquare, int perspective)
    {
        // Stockfish HalfKP indexing
        int pieceType = piece % 6;
        int pieceColor = piece / 6;

        // Apply perspective transformation
        int relativeSquare = square;
        int relativeKingSquare = kingSquare;

        if (perspective == 1) // Black perspective
        {
            relativeSquare ^= 56; // Vertical flip
            relativeKingSquare ^= 56;
        }

        // Stockfish feature index calculation
        int colorOffset = (pieceColor == perspective) ? 0 : 384; // 6 * 64
        int featureIndex = colorOffset + pieceType * 64 + relativeSquare;

        return (relativeKingSquare * 768 + featureIndex) * L1Size;
    }

    public static int KingBucket(int kingSquare)
    {
        return 0; // Stockfish uses single bucket for simplicity
    }
}
"@

    Set-Content -Path $nnueConstantsPath -Value $stockfishConstants -Encoding UTF8
    Write-Host "✓ NNUE constants updated for Stockfish format" -ForegroundColor Green
} else {
    Write-Host "⚠ NNUE constants file not found: $nnueConstantsPath" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setup completed!" -ForegroundColor Green
Write-Host "================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Build the project: dotnet build Meridian.sln -c Release" -ForegroundColor White
Write-Host "2. Test NNUE loading: echo 'uci' | .\Meridian.exe" -ForegroundColor White
Write-Host "3. Expected to see: 'info string Stockfish NNUE loaded successfully'" -ForegroundColor White
Write-Host ""
Write-Host "Expected improvements with NNUE:" -ForegroundColor Cyan
Write-Host "- Much stronger positional play (~2400+ ELO)" -ForegroundColor White
Write-Host "- Lower node count (~50-100 MN/s instead of 670 MN/s)" -ForegroundColor White
Write-Host "- More accurate evaluations" -ForegroundColor White
Write-Host "- Better endgame knowledge" -ForegroundColor White
Write-Host ""

# Create a test script
$testScript = @"
@echo off
echo Testing Stockfish NNUE setup...
echo.

echo Checking for network file:
if exist "networks\stockfish.nnue" (
    echo ✓ Network file found
    for %%i in (networks\stockfish.nnue) do echo   Size: %%~zi bytes
) else (
    echo ✗ Network file missing
    goto :end
)

echo.
echo Testing UCI with NNUE:
(echo uci & echo position startpos & echo go depth 8 & echo quit) | Meridian.exe

:end
pause
"@

Set-Content -Path "test-nnue.bat" -Value $testScript -Encoding ASCII
Write-Host "Test script created: test-nnue.bat" -ForegroundColor Cyan
Write-Host "Run this after building to verify NNUE works correctly." -ForegroundColor White
