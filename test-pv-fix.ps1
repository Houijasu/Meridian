# Test script to verify the principal variation fix
# This script will test the engine with a simple depth search to verify PV output

Write-Host "Testing PV fix for Meridian chess engine..."

# Change to the Meridian directory
Set-Location "Meridian"

# Build the solution
Write-Host "Building solution..."
dotnet build Meridian.sln

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Test the engine with a simple depth search
Write-Host "Testing engine with depth 8 search..."

$testCommands = @(
    "uci",
    "isready",
    "position startpos",
    "go depth 8",
    "quit"
)

# Join commands with newlines and pipe to the engine
$testInput = $testCommands -join "`n"

$result = $testInput | dotnet run --project Meridian/Meridian

Write-Host "Engine output:" -ForegroundColor Yellow
Write-Host $result

# Check if PV is present for each depth
$infoLines = $result -split "`n" | Where-Object { $_ -match "^info depth" }

Write-Host "`nAnalyzing PV output:" -ForegroundColor Cyan

$pvMissingCount = 0
$totalDepths = 0

foreach ($line in $infoLines) {
    if ($line -match "info depth (\d+)") {
        $depth = $matches[1]
        $totalDepths++

        if ($line -match " pv ") {
            Write-Host "Depth $depth : PV present" -ForegroundColor Green
        } else {
            Write-Host "Depth $depth : PV missing" -ForegroundColor Red
            $pvMissingCount++
        }
    }
}

Write-Host "`nSummary:" -ForegroundColor Yellow
Write-Host "Total depths: $totalDepths"
Write-Host "PV missing: $pvMissingCount"
Write-Host "PV present: $($totalDepths - $pvMissingCount)"

if ($pvMissingCount -eq 0) {
    Write-Host "SUCCESS: All depths have PV output!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAILURE: $pvMissingCount depths missing PV output" -ForegroundColor Red
    exit 1
}
