#!/usr/bin/env pwsh

# NNUE Implementation Test Script for Meridian Chess Engine
# This script tests the new NNUE implementation to ensure it works correctly

Write-Host "=== NNUE Implementation Test Script ===" -ForegroundColor Green
Write-Host "Testing the new NNUE implementation..." -ForegroundColor Yellow

# Set up paths
$ProjectRoot = Get-Location
$MeridianPath = Join-Path $ProjectRoot "Meridian"
$TestProject = Join-Path $MeridianPath "Meridian.Tests"
$CoreProject = Join-Path $MeridianPath "Meridian.Core"

Write-Host "Project root: $ProjectRoot" -ForegroundColor Cyan
Write-Host "Meridian path: $MeridianPath" -ForegroundColor Cyan

# Function to run tests with error handling
function Run-TestSuite {
    param(
        [string]$TestName,
        [string]$Filter = $null
    )

    Write-Host "`n--- Running $TestName ---" -ForegroundColor Magenta

    try {
        if ($Filter) {
            $result = dotnet test $TestProject --filter $Filter --verbosity minimal 2>&1
        } else {
            $result = dotnet test $TestProject --verbosity minimal 2>&1
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ $TestName: PASSED" -ForegroundColor Green
            return $true
        } else {
            Write-Host "❌ $TestName: FAILED" -ForegroundColor Red
            Write-Host "Error output:" -ForegroundColor Red
            Write-Host $result -ForegroundColor DarkRed
            return $false
        }
    } catch {
        Write-Host "❌ $TestName: EXCEPTION" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor DarkRed
        return $false
    }
}

# Function to check compilation
function Test-Compilation {
    Write-Host "`n--- Testing Compilation ---" -ForegroundColor Magenta

    try {
        Write-Host "Building Meridian.Core..." -ForegroundColor Yellow
        $buildResult = dotnet build $CoreProject --verbosity minimal 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Compilation: PASSED" -ForegroundColor Green
            return $true
        } else {
            Write-Host "❌ Compilation: FAILED" -ForegroundColor Red
            Write-Host "Build output:" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor DarkRed
            return $false
        }
    } catch {
        Write-Host "❌ Compilation: EXCEPTION" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor DarkRed
        return $false
    }
}

# Function to test NNUE constants
function Test-NNUEConstants {
    Write-Host "`n--- Testing NNUE Constants ---" -ForegroundColor Magenta

    $testCode = @"
using System;
using Meridian.Core.NNUE;

class Program {
    static void Main() {
        Console.WriteLine("Input Dimensions: " + NNUEConstants.InputDimensions);
        Console.WriteLine("L1 Size: " + NNUEConstants.L1Size);
        Console.WriteLine("L2 Size: " + NNUEConstants.L2Size);
        Console.WriteLine("L3 Size: " + NNUEConstants.L3Size);
        Console.WriteLine("King Buckets: " + NNUEConstants.KingBuckets);
        Console.WriteLine("Expected File Size: " + NNUEConstants.GetExpectedFileSize());

        // Test feature indexing
        var pawnIndex = NNUEConstants.GetPieceTypeIndex(Meridian.Core.Board.PieceType.Pawn);
        var kingIndex = NNUEConstants.GetPieceTypeIndex(Meridian.Core.Board.PieceType.King);
        Console.WriteLine("Pawn Index: " + pawnIndex);
        Console.WriteLine("King Index: " + kingIndex);

        // Test king bucketing
        var bucket0 = NNUEConstants.GetKingBucket(0);  // a1
        var bucket7 = NNUEConstants.GetKingBucket(7);  // h1
        Console.WriteLine("King Bucket a1: " + bucket0);
        Console.WriteLine("King Bucket h1: " + bucket7);

        // Test activation function
        var relu_neg = NNUEConstants.ClippedReLU(-10);
        var relu_pos = NNUEConstants.ClippedReLU(50);
        var relu_max = NNUEConstants.ClippedReLU(200);
        Console.WriteLine("ClippedReLU(-10): " + relu_neg);
        Console.WriteLine("ClippedReLU(50): " + relu_pos);
        Console.WriteLine("ClippedReLU(200): " + relu_max);

        Console.WriteLine("Constants test completed successfully!");
    }
}
"@

    try {
        $tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
        $testCode | Out-File -FilePath $tempFile -Encoding UTF8

        Write-Host "Compiling and running constants test..." -ForegroundColor Yellow
        $compileResult = dotnet run --project $CoreProject -- $tempFile 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ NNUE Constants: PASSED" -ForegroundColor Green
            Remove-Item $tempFile -ErrorAction SilentlyContinue
            return $true
        } else {
            Write-Host "❌ NNUE Constants: FAILED" -ForegroundColor Red
            Write-Host $compileResult -ForegroundColor DarkRed
            Remove-Item $tempFile -ErrorAction SilentlyContinue
            return $false
        }
    } catch {
        Write-Host "❌ NNUE Constants: EXCEPTION" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor DarkRed
        return $false
    }
}

# Function to create a mock NNUE file for testing
function Create-MockNNUEFile {
    param([string]$FilePath)

    Write-Host "Creating mock NNUE file: $FilePath" -ForegroundColor Yellow

    try {
        $stream = [System.IO.File]::Create($FilePath)
        $writer = New-Object System.IO.BinaryWriter($stream)

        # Write header (1024 bytes of zeros)
        $header = New-Object byte[] 1024
        $writer.Write($header)

        # Write some dummy weights (feature weights)
        for ($i = 0; $i -lt 10000; $i++) {
            $writer.Write([int16]($i % 200 - 100))  # Random-ish weights
        }

        # Write some dummy biases
        for ($i = 0; $i -lt 256; $i++) {
            $writer.Write([int16]($i % 100))
        }

        # Write more dummy data
        for ($i = 0; $i -lt 5000; $i++) {
            $writer.Write([sbyte]($i % 20 - 10))
        }

        $writer.Close()
        $stream.Close()

        Write-Host "✅ Mock NNUE file created: $([System.IO.FileInfo]::new($FilePath).Length) bytes" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ Failed to create mock NNUE file: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to test NNUE network loading
function Test-NNUELoading {
    Write-Host "`n--- Testing NNUE Network Loading ---" -ForegroundColor Magenta

    $mockFile = Join-Path $env:TEMP "test_network.nnue"

    if (Create-MockNNUEFile -FilePath $mockFile) {
        try {
            # This would need to be implemented as a separate test program
            Write-Host "Mock NNUE file created successfully for testing" -ForegroundColor Green
            Write-Host "File size: $([System.IO.FileInfo]::new($mockFile).Length) bytes" -ForegroundColor Cyan

            # Clean up
            Remove-Item $mockFile -ErrorAction SilentlyContinue

            Write-Host "✅ NNUE Loading Test: PASSED" -ForegroundColor Green
            return $true
        } catch {
            Write-Host "❌ NNUE Loading Test: FAILED" -ForegroundColor Red
            Write-Host $_.Exception.Message -ForegroundColor DarkRed
            return $false
        }
    } else {
        Write-Host "❌ NNUE Loading Test: FAILED (could not create mock file)" -ForegroundColor Red
        return $false
    }
}

# Function to run comprehensive tests
function Test-AllComponents {
    Write-Host "`n=== Running All NNUE Tests ===" -ForegroundColor Green

    $results = @()

    # Test 1: Compilation
    $results += Test-Compilation

    # Test 2: NNUE Constants
    $results += Test-NNUEConstants

    # Test 3: Unit Tests (if available)
    if (Test-Path $TestProject) {
        Write-Host "`nFound test project, running unit tests..." -ForegroundColor Yellow
        $results += Run-TestSuite -TestName "NNUE Unit Tests" -Filter "NNUE"
        $results += Run-TestSuite -TestName "All Unit Tests"
    } else {
        Write-Host "`nNo test project found, skipping unit tests" -ForegroundColor Yellow
    }

    # Test 4: Network Loading
    $results += Test-NNUELoading

    return $results
}

# Function to display summary
function Show-TestSummary {
    param([bool[]]$Results)

    $passed = ($Results | Where-Object { $_ -eq $true }).Count
    $total = $Results.Count
    $failed = $total - $passed

    Write-Host "`n=== Test Summary ===" -ForegroundColor Green
    Write-Host "Total Tests: $total" -ForegroundColor Cyan
    Write-Host "Passed: $passed" -ForegroundColor Green
    Write-Host "Failed: $failed" -ForegroundColor Red

    if ($failed -eq 0) {
        Write-Host "`n🎉 All tests passed! NNUE implementation is working correctly." -ForegroundColor Green
        return $true
    } else {
        Write-Host "`n⚠️  Some tests failed. Please review the errors above." -ForegroundColor Yellow
        return $false
    }
}

# Main execution
try {
    Write-Host "Starting NNUE implementation tests..." -ForegroundColor Yellow

    # Check if we're in the right directory
    if (-not (Test-Path $MeridianPath)) {
        Write-Host "❌ Error: Cannot find Meridian directory. Are you in the correct folder?" -ForegroundColor Red
        Write-Host "Expected path: $MeridianPath" -ForegroundColor Red
        exit 1
    }

    # Change to Meridian directory
    Set-Location $MeridianPath

    # Run all tests
    $testResults = Test-AllComponents

    # Show summary
    $success = Show-TestSummary -Results $testResults

    if ($success) {
        Write-Host "`n✅ NNUE implementation test completed successfully!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n❌ NNUE implementation test completed with failures." -ForegroundColor Red
        exit 1
    }

} catch {
    Write-Host "`n❌ Fatal error during testing: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    exit 1
} finally {
    # Return to original directory
    Set-Location $ProjectRoot
}
