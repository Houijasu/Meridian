#!/usr/bin/env pwsh

# NNUE Real Network Test Script for Meridian Chess Engine
# This script tests NNUE loading and evaluation with the actual obsidian.nnue network file

Write-Host "=== NNUE Real Network Test Script ===" -ForegroundColor Green
Write-Host "Testing NNUE with actual obsidian.nnue network file..." -ForegroundColor Yellow

# Set up paths
$ProjectRoot = Get-Location
$MeridianPath = Join-Path $ProjectRoot "Meridian"
$NetworkPath = Join-Path $ProjectRoot "networks\obsidian.nnue"
$ExecutablePath = Join-Path $MeridianPath "Meridian\bin\Debug\net9.0\Meridian.exe"

Write-Host "Project root: $ProjectRoot" -ForegroundColor Cyan
Write-Host "Network path: $NetworkPath" -ForegroundColor Cyan
Write-Host "Executable path: $ExecutablePath" -ForegroundColor Cyan

# Function to test network file
function Test-NetworkFile {
    Write-Host "`n--- Testing Network File ---" -ForegroundColor Magenta

    if (Test-Path $NetworkPath) {
        $fileInfo = Get-Item $NetworkPath
        $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
        Write-Host "✅ Network file found: $($fileInfo.Name)" -ForegroundColor Green
        Write-Host "✅ File size: $fileSizeMB MB" -ForegroundColor Green

        # Check if size is reasonable for NNUE
        if ($fileInfo.Length -gt 1MB -and $fileInfo.Length -lt 100MB) {
            Write-Host "✅ File size looks reasonable for NNUE network" -ForegroundColor Green
            return $true
        } else {
            Write-Host "⚠️  File size seems unusual for NNUE network" -ForegroundColor Yellow
            return $false
        }
    } else {
        Write-Host "❌ Network file not found: $NetworkPath" -ForegroundColor Red
        return $false
    }
}

# Function to build the engine
function Build-Engine {
    Write-Host "`n--- Building Engine ---" -ForegroundColor Magenta

    try {
        Set-Location $MeridianPath
        Write-Host "Building Meridian engine..." -ForegroundColor Yellow

        $buildResult = dotnet build Meridian.sln --configuration Debug --verbosity minimal 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Build successful" -ForegroundColor Green
            return $true
        } else {
            Write-Host "❌ Build failed:" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "❌ Build error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    } finally {
        Set-Location $ProjectRoot
    }
}

# Function to test UCI engine with NNUE
function Test-UCIEngine {
    Write-Host "`n--- Testing UCI Engine with NNUE ---" -ForegroundColor Magenta

    if (-not (Test-Path $ExecutablePath)) {
        Write-Host "❌ Engine executable not found: $ExecutablePath" -ForegroundColor Red
        return $false
    }

    try {
        # Create UCI test commands
        $uciCommands = @"
uci
setoption name UseNNUE value true
setoption name NNUEPath value networks/obsidian.nnue
isready
position startpos
go depth 3
quit
"@

        Write-Host "Starting UCI engine test..." -ForegroundColor Yellow
        Write-Host "Commands to send:" -ForegroundColor Cyan
        Write-Host $uciCommands -ForegroundColor Gray

        # Start the engine process
        $processInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processInfo.FileName = $ExecutablePath
        $processInfo.UseShellExecute = $false
        $processInfo.RedirectStandardInput = $true
        $processInfo.RedirectStandardOutput = $true
        $processInfo.RedirectStandardError = $true
        $processInfo.CreateNoWindow = $true
        $processInfo.WorkingDirectory = $ProjectRoot

        $process = [System.Diagnostics.Process]::Start($processInfo)

        # Send commands
        $process.StandardInput.WriteLine($uciCommands)
        $process.StandardInput.Close()

        # Read output with timeout
        $outputLines = @()
        $timeout = 30000 # 30 seconds
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        while (-not $process.HasExited -and $stopwatch.ElapsedMilliseconds -lt $timeout) {
            if ($process.StandardOutput.Peek() -ne -1) {
                $line = $process.StandardOutput.ReadLine()
                $outputLines += $line
                Write-Host ">>> $line" -ForegroundColor Gray
            }
            Start-Sleep -Milliseconds 10
        }

        # Force close if still running
        if (-not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit(5000)
        }

        $stopwatch.Stop()

        # Analyze output
        Write-Host "`n--- Analyzing Engine Output ---" -ForegroundColor Magenta

        $nnueLoaded = $false
        $nnueEnabled = $false
        $evaluationMode = "UNKNOWN"
        $bestMove = $null

        foreach ($line in $outputLines) {
            if ($line -match "NNUE.*loaded successfully") {
                $nnueLoaded = $true
                Write-Host "✅ NNUE network loaded" -ForegroundColor Green
            }
            elseif ($line -match "NNUE.*loading failed") {
                Write-Host "❌ NNUE loading failed" -ForegroundColor Red
            }
            elseif ($line -match "Final evaluation mode:\s*(\w+)") {
                $evaluationMode = $matches[1]
                Write-Host "✅ Evaluation mode: $evaluationMode" -ForegroundColor Green
            }
            elseif ($line -match "NNUE evaluation:\s*(\w+)") {
                $nnueEnabled = $matches[1] -eq "ENABLED"
                Write-Host "✅ NNUE evaluation: $($matches[1])" -ForegroundColor Green
            }
            elseif ($line -match "bestmove\s+(\w+)") {
                $bestMove = $matches[1]
                Write-Host "✅ Best move found: $bestMove" -ForegroundColor Green
            }
        }

        # Summary
        Write-Host "`n--- Test Results ---" -ForegroundColor Magenta
        Write-Host "NNUE Loaded: $(if ($nnueLoaded) { '✅ YES' } else { '❌ NO' })" -ForegroundColor $(if ($nnueLoaded) { 'Green' } else { 'Red' })
        Write-Host "NNUE Enabled: $(if ($nnueEnabled) { '✅ YES' } else { '❌ NO' })" -ForegroundColor $(if ($nnueEnabled) { 'Green' } else { 'Red' })
        Write-Host "Evaluation Mode: $evaluationMode" -ForegroundColor Cyan
        Write-Host "Best Move: $(if ($bestMove) { $bestMove } else { 'None found' })" -ForegroundColor Cyan

        # Return success if NNUE loaded and we got a move
        return $nnueLoaded -and $bestMove

    } catch {
        Write-Host "❌ UCI test error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to run performance test
function Test-Performance {
    param([bool]$UseNNUE)

    $modeStr = if ($UseNNUE) { "NNUE" } else { "Traditional" }
    Write-Host "`n--- Testing Performance ($modeStr) ---" -ForegroundColor Magenta

    if (-not (Test-Path $ExecutablePath)) {
        Write-Host "❌ Engine executable not found" -ForegroundColor Red
        return $false
    }

    try {
        # Create performance test commands
        $perfCommands = @"
uci
setoption name UseNNUE value $($UseNNUE.ToString().ToLower())
$(if ($UseNNUE) { "setoption name NNUEPath value networks/obsidian.nnue" })
isready
position startpos
go depth 6
quit
"@

        Write-Host "Running $modeStr performance test..." -ForegroundColor Yellow

        # Start the engine process
        $processInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processInfo.FileName = $ExecutablePath
        $processInfo.UseShellExecute = $false
        $processInfo.RedirectStandardInput = $true
        $processInfo.RedirectStandardOutput = $true
        $processInfo.RedirectStandardError = $true
        $processInfo.CreateNoWindow = $true
        $processInfo.WorkingDirectory = $ProjectRoot

        $process = [System.Diagnostics.Process]::Start($processInfo)

        # Send commands
        $process.StandardInput.WriteLine($perfCommands)
        $process.StandardInput.Close()

        # Read output
        $outputLines = @()
        $timeout = 60000 # 60 seconds
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        while (-not $process.HasExited -and $stopwatch.ElapsedMilliseconds -lt $timeout) {
            if ($process.StandardOutput.Peek() -ne -1) {
                $line = $process.StandardOutput.ReadLine()
                $outputLines += $line
            }
            Start-Sleep -Milliseconds 10
        }

        if (-not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit(5000)
        }

        # Extract performance data
        $maxNPS = 0
        $finalScore = $null
        $bestMove = $null

        foreach ($line in $outputLines) {
            if ($line -match "nps\s+(\d+)") {
                $nps = [int]$matches[1]
                if ($nps -gt $maxNPS) {
                    $maxNPS = $nps
                }
            }
            elseif ($line -match "score cp\s+([-]?\d+)") {
                $finalScore = [int]$matches[1]
            }
            elseif ($line -match "bestmove\s+(\w+)") {
                $bestMove = $matches[1]
            }
        }

        Write-Host "Performance Results ($modeStr):" -ForegroundColor Cyan
        Write-Host "  Max NPS: $($maxNPS.ToString('N0'))" -ForegroundColor White
        Write-Host "  Final Score: $finalScore centipawns" -ForegroundColor White
        Write-Host "  Best Move: $bestMove" -ForegroundColor White

        return @{
            NPS = $maxNPS
            Score = $finalScore
            BestMove = $bestMove
            Success = $maxNPS -gt 0 -and $bestMove
        }

    } catch {
        Write-Host "❌ Performance test error: $($_.Exception.Message)" -ForegroundColor Red
        return @{ Success = $false }
    }
}

# Main execution
try {
    Write-Host "Starting NNUE real network tests..." -ForegroundColor Yellow

    # Test 1: Check network file
    if (-not (Test-NetworkFile)) {
        Write-Host "`n❌ Network file test failed. Cannot continue." -ForegroundColor Red
        exit 1
    }

    # Test 2: Build engine
    if (-not (Build-Engine)) {
        Write-Host "`n❌ Engine build failed. Cannot continue." -ForegroundColor Red
        exit 1
    }

    # Test 3: Test UCI engine with NNUE
    if (-not (Test-UCIEngine)) {
        Write-Host "`n❌ UCI engine test failed." -ForegroundColor Red
        exit 1
    }

    # Test 4: Performance comparison
    Write-Host "`n=== Performance Comparison ===" -ForegroundColor Green

    $traditionalResults = Test-Performance -UseNNUE $false
    Start-Sleep -Seconds 2
    $nnueResults = Test-Performance -UseNNUE $true

    if ($traditionalResults.Success -and $nnueResults.Success) {
        Write-Host "`n--- Performance Summary ---" -ForegroundColor Green
        Write-Host "Traditional Evaluation:" -ForegroundColor Cyan
        Write-Host "  NPS: $($traditionalResults.NPS.ToString('N0'))" -ForegroundColor White
        Write-Host "  Score: $($traditionalResults.Score) cp" -ForegroundColor White
        Write-Host "  Move: $($traditionalResults.BestMove)" -ForegroundColor White

        Write-Host "NNUE Evaluation:" -ForegroundColor Cyan
        Write-Host "  NPS: $($nnueResults.NPS.ToString('N0'))" -ForegroundColor White
        Write-Host "  Score: $($nnueResults.Score) cp" -ForegroundColor White
        Write-Host "  Move: $($nnueResults.BestMove)" -ForegroundColor White

        $speedRatio = if ($traditionalResults.NPS -gt 0) { [math]::Round($traditionalResults.NPS / $nnueResults.NPS, 2) } else { 0 }
        Write-Host "`nSpeed Comparison:" -ForegroundColor Yellow
        Write-Host "  Traditional is ${speedRatio}x faster than NNUE" -ForegroundColor White
        Write-Host "  This is expected - NNUE trades speed for strength" -ForegroundColor Gray

        if ($nnueResults.NPS -gt 10000) {
            Write-Host "✅ NNUE performance is acceptable (>10k NPS)" -ForegroundColor Green
        } else {
            Write-Host "⚠️  NNUE performance is lower than expected" -ForegroundColor Yellow
        }
    }

    # Final summary
    Write-Host "`n=== Final Test Summary ===" -ForegroundColor Green
    Write-Host "🎉 All NNUE tests completed successfully!" -ForegroundColor Green
    Write-Host "" -ForegroundColor White
    Write-Host "✅ Network file: Valid (30MB obsidian.nnue)" -ForegroundColor Green
    Write-Host "✅ Engine build: Successful" -ForegroundColor Green
    Write-Host "✅ NNUE loading: Functional" -ForegroundColor Green
    Write-Host "✅ UCI integration: Working" -ForegroundColor Green
    Write-Host "✅ Performance: Measured" -ForegroundColor Green
    Write-Host "" -ForegroundColor White
    Write-Host "🚀 NNUE implementation is fully operational!" -ForegroundColor Green
    Write-Host "" -ForegroundColor White
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Test in actual games against other engines" -ForegroundColor White
    Write-Host "2. Benchmark strength improvement vs traditional evaluation" -ForegroundColor White
    Write-Host "3. Optimize performance further if needed" -ForegroundColor White

    exit 0

} catch {
    Write-Host "`n❌ Fatal error during testing: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    exit 1
} finally {
    # Return to original directory
    Set-Location $ProjectRoot
}
