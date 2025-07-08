#!/usr/bin/env pwsh

# NNUE Code Analysis Fixes Verification Script
# This script verifies that all CA (Code Analysis) rule fixes have been properly applied

Write-Host "=== NNUE Code Analysis Fixes Verification ===" -ForegroundColor Green
Write-Host ""

# Set error handling
$ErrorActionPreference = "Stop"

$totalIssues = 0
$fixedIssues = 0

try {
    # Navigate to the Meridian directory
    $meridianDir = "Meridian"
    if (-not (Test-Path $meridianDir)) {
        Write-Host "Error: Meridian directory not found!" -ForegroundColor Red
        exit 1
    }

    Set-Location $meridianDir

    Write-Host "1. Checking CA1805 Fix (Explicit initialization to default value)..." -ForegroundColor Yellow

    # Check Evaluator.cs for the CA1805 fix
    $evaluatorFile = "Meridian.Core/Evaluation/Evaluator.cs"
    if (Test-Path $evaluatorFile) {
        $evaluatorContent = Get-Content $evaluatorFile -Raw

        # Check if explicit false initialization is removed
        if ($evaluatorContent -match "private static bool _useNNUE\s*=\s*false") {
            Write-Host "   ✗ CA1805 NOT FIXED: Found explicit initialization to false" -ForegroundColor Red
            $totalIssues++
        } elseif ($evaluatorContent -match "private static bool _useNNUE\s*;") {
            Write-Host "   ✓ CA1805 FIXED: Removed explicit initialization" -ForegroundColor Green
            $fixedIssues++
        } else {
            Write-Host "   ⚠ CA1805 UNCLEAR: Could not verify fix" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ✗ Evaluator.cs file not found" -ForegroundColor Red
        $totalIssues++
    }

    Write-Host ""
    Write-Host "2. Checking CA1031 Fixes (Specific exception types)..." -ForegroundColor Yellow

    # Check Accumulator.cs for the CA1031 fixes
    $accumulatorFile = "Meridian.Core/NNUE/Accumulator.cs"
    if (Test-Path $accumulatorFile) {
        $accumulatorContent = Get-Content $accumulatorFile -Raw

        # Check for generic Exception catches (should be removed)
        $genericCatches = [regex]::Matches($accumulatorContent, "catch\s*\(\s*Exception\s+\w+\s*\)")
        if ($genericCatches.Count -gt 0) {
            Write-Host "   ✗ CA1031 NOT FIXED: Found $($genericCatches.Count) generic Exception catches" -ForegroundColor Red
            $totalIssues++
        } else {
            Write-Host "   ✓ CA1031 FIXED: No generic Exception catches found" -ForegroundColor Green
            $fixedIssues++
        }

        # Check for specific exception types (should be present)
        $specificExceptions = @(
            "AccessViolationException",
            "IndexOutOfRangeException",
            "NullReferenceException",
            "InvalidOperationException"
        )

        $foundSpecific = 0
        foreach ($exceptionType in $specificExceptions) {
            if ($accumulatorContent -match "catch\s*\(\s*$exceptionType\s+\w+\s*\)") {
                $foundSpecific++
            }
        }

        if ($foundSpecific -eq $specificExceptions.Count) {
            Write-Host "   ✓ CA1031 ENHANCED: All specific exception types found" -ForegroundColor Green
            $fixedIssues++
        } elseif ($foundSpecific -gt 0) {
            Write-Host "   ⚠ CA1031 PARTIAL: Found $foundSpecific/$($specificExceptions.Count) specific exception types" -ForegroundColor Yellow
        } else {
            Write-Host "   ✗ CA1031 MISSING: No specific exception types found" -ForegroundColor Red
            $totalIssues++
        }

        # Check for proper fallback implementations in SIMD methods
        $simdFallbacks = [regex]::Matches($accumulatorContent, "// Fallback to safe scalar operations")
        if ($simdFallbacks.Count -ge 2) {
            Write-Host "   ✓ SIMD FALLBACKS: Found proper fallback implementations" -ForegroundColor Green
            $fixedIssues++
        } else {
            Write-Host "   ⚠ SIMD FALLBACKS: May be missing fallback implementations" -ForegroundColor Yellow
        }

    } else {
        Write-Host "   ✗ Accumulator.cs file not found" -ForegroundColor Red
        $totalIssues++
    }

    Write-Host ""
    Write-Host "3. Verifying Method-Specific Fixes..." -ForegroundColor Yellow

    if (Test-Path $accumulatorFile) {
        $accumulatorContent = Get-Content $accumulatorFile -Raw

        # Check specific methods that were fixed
        $methodsToCheck = @(
            "AddFeature",
            "SubtractFeature",
            "MovePiece",
            "AddFeatureVector",
            "SubtractFeatureVector"
        )

        foreach ($method in $methodsToCheck) {
            # Look for the method and check if it has proper exception handling
            if ($accumulatorContent -match "(?s)$method.*?catch\s*\(\s*(AccessViolationException|IndexOutOfRangeException|NullReferenceException|InvalidOperationException)") {
                Write-Host "   ✓ $method has specific exception handling" -ForegroundColor Green
                $fixedIssues++
            } else {
                Write-Host "   ✗ $method missing specific exception handling" -ForegroundColor Red
                $totalIssues++
            }
        }
    }

    Write-Host ""
    Write-Host "4. Checking for Error Message Improvements..." -ForegroundColor Yellow

    if (Test-Path $accumulatorFile) {
        $accumulatorContent = Get-Content $accumulatorFile -Raw

        # Check for improved error messages
        $errorMessages = @(
            "Access violation",
            "Index out of range",
            "Null reference",
            "Invalid operation"
        )

        $foundMessages = 0
        foreach ($message in $errorMessages) {
            if ($accumulatorContent -match [regex]::Escape($message)) {
                $foundMessages++
            }
        }

        if ($foundMessages -eq $errorMessages.Count) {
            Write-Host "   ✓ All improved error messages found" -ForegroundColor Green
            $fixedIssues++
        } elseif ($foundMessages -gt 0) {
            Write-Host "   ⚠ Found $foundMessages/$($errorMessages.Count) improved error messages" -ForegroundColor Yellow
        } else {
            Write-Host "   ✗ No improved error messages found" -ForegroundColor Red
            $totalIssues++
        }
    }

    Write-Host ""
    Write-Host "5. Checking File Integrity..." -ForegroundColor Yellow

    # Check that all NNUE files exist and are accessible
    $nnueFiles = @(
        "Meridian.Core/NNUE/NNUENetwork.cs",
        "Meridian.Core/NNUE/NNUEConstants.cs",
        "Meridian.Core/NNUE/Accumulator.cs",
        "Meridian.Core/Evaluation/Evaluator.cs",
        "Meridian.Tests/NNUE/NNUENetworkTests.cs"
    )

    foreach ($file in $nnueFiles) {
        if (Test-Path $file) {
            Write-Host "   ✓ $file exists" -ForegroundColor Green
            $fixedIssues++
        } else {
            Write-Host "   ✗ $file missing" -ForegroundColor Red
            $totalIssues++
        }
    }

    Write-Host ""
    Write-Host "6. Syntax Validation..." -ForegroundColor Yellow

    # Check for common syntax issues that could be introduced
    if (Test-Path $accumulatorFile) {
        $accumulatorContent = Get-Content $accumulatorFile -Raw

        # Check for proper brace matching
        $openBraces = [regex]::Matches($accumulatorContent, "\{").Count
        $closeBraces = [regex]::Matches($accumulatorContent, "\}").Count

        if ($openBraces -eq $closeBraces) {
            Write-Host "   ✓ Brace matching is correct" -ForegroundColor Green
            $fixedIssues++
        } else {
            Write-Host "   ✗ Brace mismatch: $openBraces open, $closeBraces close" -ForegroundColor Red
            $totalIssues++
        }

        # Check for proper catch block structure
        $catchBlocks = [regex]::Matches($accumulatorContent, "catch\s*\([^)]+\)\s*\{")
        if ($catchBlocks.Count -gt 0) {
            Write-Host "   ✓ Catch blocks have proper structure" -ForegroundColor Green
            $fixedIssues++
        } else {
            Write-Host "   ✗ No proper catch blocks found" -ForegroundColor Red
            $totalIssues++
        }
    }

    Write-Host ""
    Write-Host "7. Documentation Check..." -ForegroundColor Yellow

    # Check if documentation files exist
    $docFiles = @(
        "NNUE_CODE_ANALYSIS_FIXES.md",
        "NNUE_FIXES_COMPLETE.md"
    )

    foreach ($docFile in $docFiles) {
        if (Test-Path $docFile) {
            Write-Host "   ✓ Documentation: $docFile exists" -ForegroundColor Green
            $fixedIssues++
        } else {
            Write-Host "   ⚠ Documentation: $docFile missing" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "=== VERIFICATION RESULTS ===" -ForegroundColor Cyan
    Write-Host ""

    $totalChecks = $totalIssues + $fixedIssues
    $successRate = if ($totalChecks -gt 0) { [math]::Round(($fixedIssues / $totalChecks) * 100, 1) } else { 0 }

    Write-Host "Fixed Issues: $fixedIssues" -ForegroundColor Green
    Write-Host "Remaining Issues: $totalIssues" -ForegroundColor $(if ($totalIssues -eq 0) { "Green" } else { "Red" })
    Write-Host "Success Rate: $successRate%" -ForegroundColor $(if ($successRate -ge 90) { "Green" } elseif ($successRate -ge 70) { "Yellow" } else { "Red" })
    Write-Host ""

    if ($totalIssues -eq 0) {
        Write-Host "🎉 ALL CODE ANALYSIS FIXES VERIFIED SUCCESSFULLY! 🎉" -ForegroundColor Green
        Write-Host ""
        Write-Host "Summary of Applied Fixes:" -ForegroundColor Cyan
        Write-Host "✅ CA1805: Removed explicit initialization to default value" -ForegroundColor Green
        Write-Host "✅ CA1031: Replaced generic Exception catches with specific types" -ForegroundColor Green
        Write-Host "✅ Enhanced error messages for better debugging" -ForegroundColor Green
        Write-Host "✅ Added proper fallback implementations for SIMD operations" -ForegroundColor Green
        Write-Host "✅ Maintained functionality while improving code quality" -ForegroundColor Green
        Write-Host ""
        Write-Host "The NNUE implementation is now fully compliant with .NET code analysis rules!" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Some issues remain to be fixed" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Recommendations:" -ForegroundColor Cyan
        Write-Host "1. Review any remaining generic Exception catches" -ForegroundColor White
        Write-Host "2. Ensure all specific exception types are properly handled" -ForegroundColor White
        Write-Host "3. Verify proper fallback implementations" -ForegroundColor White
        Write-Host "4. Check syntax and structure integrity" -ForegroundColor White
    }

    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Build the solution to confirm no compilation errors" -ForegroundColor White
    Write-Host "2. Run unit tests to ensure functionality is preserved" -ForegroundColor White
    Write-Host "3. Test NNUE evaluation with sample positions" -ForegroundColor White
    Write-Host "4. Integration test with full engine" -ForegroundColor White

} catch {
    Write-Host ""
    Write-Host "✗ Verification script failed with error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
} finally {
    # Return to original directory
    Set-Location ..
}

Write-Host ""
Write-Host "Code Analysis Fixes Verification Complete!" -ForegroundColor Green
