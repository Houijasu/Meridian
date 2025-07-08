# Quick NNUE Loading Test for Meridian Chess Engine
# This script tests if NNUE is loading properly

Write-Host "Meridian NNUE Quick Test" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

# Check current directory
$currentDir = Get-Location
Write-Host "Current directory: $currentDir" -ForegroundColor Cyan

# Check for network file
$networkPath = "networks\obsidian.nnue"
Write-Host "Checking for NNUE network..." -ForegroundColor Yellow

if (Test-Path $networkPath) {
    $fileSize = (Get-Item $networkPath).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    Write-Host "✓ Found: $networkPath ($fileSizeMB MB)" -ForegroundColor Green
} else {
    Write-Host "✗ Not found: $networkPath" -ForegroundColor Red
    Write-Host "Please ensure the file exists before testing." -ForegroundColor Yellow
    exit 1
}

# Check for solution file
if (!(Test-Path "Meridian\Meridian.sln")) {
    Write-Host "✗ Solution file not found. Run from project root." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Building project..." -ForegroundColor Yellow

# Build the project
Set-Location "Meridian"
$buildResult = dotnet build Meridian.sln -c Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Create test input
$testInput = @"
uci
position startpos
go depth 6
quit
"@

Write-Host "Testing NNUE loading..." -ForegroundColor Yellow
Write-Host "Expected if NNUE works: ~50-200 MN/s" -ForegroundColor Cyan
Write-Host "Expected if Traditional: ~500+ MN/s" -ForegroundColor Cyan
Write-Host ""

# Run the test
$output = $testInput | dotnet run --project Meridian\Meridian\Meridian.csproj 2>&1

# Display startup messages
Write-Host "Engine Startup Messages:" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow
$output | Where-Object { $_ -like "*NNUE*" -or $_ -like "*evaluation*" -or $_ -like "*directory*" } | ForEach-Object {
    if ($_ -like "*SUCCESS*") {
        Write-Host $_ -ForegroundColor Green
    } elseif ($_ -like "*FAILED*" -or $_ -like "*ERROR*") {
        Write-Host $_ -ForegroundColor Red
    } else {
        Write-Host $_ -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Performance Results:" -ForegroundColor Yellow
Write-Host "-------------------" -ForegroundColor Yellow

# Extract performance data
$perfLines = $output | Where-Object { $_ -like "*MN*" -or $_ -like "*kN*" }
if ($perfLines) {
    $perfLines | ForEach-Object {
        Write-Host $_ -ForegroundColor Cyan

        # Analyze node count
        if ($_ -match "(\d+)MN") {
            $nodeCount = [int]$matches[1]
            if ($nodeCount -gt 300) {
                Write-Host "  → HIGH node count detected - Traditional evaluation likely" -ForegroundColor Yellow
            } elseif ($nodeCount -lt 200) {
                Write-Host "  → Lower node count - NNUE might be working!" -ForegroundColor Green
            }
        }
    }
} else {
    Write-Host "No performance data found in output" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow

# Check for NNUE success
if ($output -like "*SUCCESS: NNUE loaded*") {
    Write-Host "✓ NNUE loading: SUCCESS" -ForegroundColor Green
} elseif ($output -like "*FAILED*" -or $output -like "*ERROR*") {
    Write-Host "✗ NNUE loading: FAILED" -ForegroundColor Red
} else {
    Write-Host "? NNUE loading: UNCLEAR" -ForegroundColor Yellow
}

# Check final evaluation mode
$evalMode = $output | Where-Object { $_ -like "*Final evaluation mode*" }
if ($evalMode) {
    if ($evalMode -like "*NNUE*") {
        Write-Host "✓ Evaluation mode: NNUE" -ForegroundColor Green
    } else {
        Write-Host "✗ Evaluation mode: TRADITIONAL" -ForegroundColor Red
    }
} else {
    Write-Host "? Evaluation mode: UNKNOWN" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Full output saved to: nnue_test_output.txt" -ForegroundColor Cyan
$output | Out-File -FilePath "nnue_test_output.txt" -Encoding UTF8

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
if ($output -like "*SUCCESS: NNUE loaded*") {
    Write-Host "- NNUE appears to be working!" -ForegroundColor Green
    Write-Host "- Test with deeper searches to verify strength" -ForegroundColor White
} else {
    Write-Host "- NNUE is not loading properly" -ForegroundColor Red
    Write-Host "- Check the detailed output above for error messages" -ForegroundColor White
    Write-Host "- Verify network file format compatibility" -ForegroundColor White
}

Set-Location ".."
