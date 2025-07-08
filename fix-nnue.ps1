# Quick Fix for NNUE Network Loading Issue
# This script copies the NNUE network file to the correct location

param(
    [string]$ExecutablePath = ".\publish\win-x64\Meridian.exe"
)

Write-Host "Meridian NNUE Network Fix Script" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""

# Find the executable directory
$execDir = Split-Path -Parent $ExecutablePath
if (!(Test-Path $execDir)) {
    Write-Host "Error: Executable directory not found: $execDir" -ForegroundColor Red
    Write-Host "Please build the engine first or specify correct path with -ExecutablePath" -ForegroundColor Yellow
    exit 1
}

# Find the NNUE network file
$networkPaths = @(
    "networks\obsidian.nnue",
    "Meridian\networks\obsidian.nnue",
    "Meridian\Meridian\networks\obsidian.nnue"
)

$networkFile = $null
foreach ($path in $networkPaths) {
    if (Test-Path $path) {
        $networkFile = $path
        break
    }
}

if (!$networkFile) {
    Write-Host "Error: NNUE network file 'obsidian.nnue' not found in any of these locations:" -ForegroundColor Red
    foreach ($path in $networkPaths) {
        Write-Host "  - $path" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host "Found NNUE network file: $networkFile" -ForegroundColor Green

# Create networks directory in executable folder
$targetDir = Join-Path $execDir "networks"
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "Created networks directory: $targetDir" -ForegroundColor Green
}

# Copy NNUE network file
$targetFile = Join-Path $targetDir "obsidian.nnue"
Copy-Item -Path $networkFile -Destination $targetFile -Force

$fileSize = [math]::Round((Get-Item $targetFile).Length / 1MB, 2)
Write-Host "Copied NNUE network file: $targetFile ($fileSize MB)" -ForegroundColor Green

Write-Host ""
Write-Host "NNUE Fix Applied Successfully!" -ForegroundColor Green
Write-Host ""

# Test the engine
Write-Host "Testing NNUE loading..." -ForegroundColor Yellow
$testResult = ""
try {
    if (Test-Path $ExecutablePath) {
        $process = Start-Process -FilePath $ExecutablePath -ArgumentList "" -NoNewWindow -PassThru -RedirectStandardInput -RedirectStandardOutput -RedirectStandardError

        $process.StandardInput.WriteLine("uci")
        $process.StandardInput.WriteLine("quit")

        $timeout = 5000
        if ($process.WaitForExit($timeout)) {
            $output = $process.StandardOutput.ReadToEnd()
            $error = $process.StandardError.ReadToEnd()

            if ($output -like "*NNUE loaded*") {
                Write-Host "✓ NNUE network loaded successfully!" -ForegroundColor Green
                $testResult = "SUCCESS"
            } elseif ($output -like "*NNUE file not found*") {
                Write-Host "✗ NNUE file still not found - check file path" -ForegroundColor Red
                $testResult = "FAILED"
            } else {
                Write-Host "? Could not determine NNUE status from output" -ForegroundColor Yellow
                $testResult = "UNKNOWN"
            }
        } else {
            $process.Kill()
            Write-Host "✗ Engine test timed out" -ForegroundColor Red
            $testResult = "TIMEOUT"
        }
    } else {
        Write-Host "✗ Engine executable not found: $ExecutablePath" -ForegroundColor Red
        $testResult = "NO_ENGINE"
    }
} catch {
    Write-Host "✗ Error testing engine: $($_.Exception.Message)" -ForegroundColor Red
    $testResult = "ERROR"
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. Run the engine: $ExecutablePath" -ForegroundColor White
Write-Host "2. Send 'uci' command and verify you see 'NNUE loaded'" -ForegroundColor White
Write-Host "3. Test with: position startpos" -ForegroundColor White
Write-Host "4. Test with: go depth 10" -ForegroundColor White
Write-Host ""

if ($testResult -eq "SUCCESS") {
    Write-Host "The engine should now play much stronger moves!" -ForegroundColor Green
    Write-Host "Expected improvements:" -ForegroundColor Yellow
    Write-Host "- Better opening moves (e4, d4, Nf3, c4 instead of Nh3)" -ForegroundColor White
    Write-Host "- More varied evaluations (not all ~0.0)" -ForegroundColor White
    Write-Host "- Stronger tactical and positional play" -ForegroundColor White
} else {
    Write-Host "Manual verification required - check engine output for NNUE status" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Script completed." -ForegroundColor Green
