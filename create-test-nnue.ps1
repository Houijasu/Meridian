#!/usr/bin/env pwsh

# Create Test NNUE Network Script
# This script creates a small, compatible NNUE network file for testing the Meridian engine

Write-Host "=== Creating Test NNUE Network ===" -ForegroundColor Green
Write-Host "Generating a small, compatible NNUE file for testing..." -ForegroundColor Yellow

# Set up paths
$ProjectRoot = Get-Location
$NetworksDir = Join-Path $ProjectRoot "networks"
$TestNetworkPath = Join-Path $NetworksDir "test.nnue"
$BackupPath = Join-Path $NetworksDir "obsidian_backup.nnue"

# Create networks directory if it doesn't exist
if (-not (Test-Path $NetworksDir)) {
    New-Item -ItemType Directory -Path $NetworksDir | Out-Null
    Write-Host "Created networks directory" -ForegroundColor Green
}

# Backup existing obsidian.nnue if it exists
$obsidianPath = Join-Path $NetworksDir "obsidian.nnue"
if (Test-Path $obsidianPath) {
    Write-Host "Backing up existing obsidian.nnue..." -ForegroundColor Yellow
    Copy-Item $obsidianPath $BackupPath -Force
    Write-Host "Backup created: obsidian_backup.nnue" -ForegroundColor Green
}

Write-Host "Creating test NNUE network file..." -ForegroundColor Yellow

try {
    # Create binary writer
    $fileStream = [System.IO.File]::Create($TestNetworkPath)
    $writer = New-Object System.IO.BinaryWriter($fileStream)

    # Network architecture constants (matching NNUEConstants.cs)
    $KingBuckets = 10
    $PieceTypes = 12  # 6 piece types * 2 colors
    $Squares = 64
    $L1Size = 1024
    $L2Size = 8
    $L3Size = 32
    $OutputSize = 1

    # Calculate sizes
    $FeatureWeightsCount = $KingBuckets * $PieceTypes * $Squares * $L1Size
    $FeatureBiasCount = $L1Size
    $L1WeightsCount = $L1Size * $L2Size
    $L1BiasCount = $L2Size
    $L2WeightsCount = $L2Size * $L3Size
    $L2BiasCount = $L3Size
    $L3WeightsCount = $L3Size * $OutputSize
    $L3BiasCount = $OutputSize

    Write-Host "Network architecture:" -ForegroundColor Cyan
    Write-Host "  Feature weights: $FeatureWeightsCount" -ForegroundColor White
    Write-Host "  L1 size: $L1Size" -ForegroundColor White
    Write-Host "  L2 size: $L2Size" -ForegroundColor White
    Write-Host "  L3 size: $L3Size" -ForegroundColor White

    # Initialize random number generator with fixed seed for reproducibility
    $random = New-Object System.Random(12345)

    # Helper function to generate random weight
    function Get-RandomWeight {
        param([int]$Scale = 100)
        return $random.Next(-$Scale, $Scale)
    }

    # Write feature weights (int16)
    Write-Host "Writing feature weights..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $FeatureWeightsCount; $i++) {
        $weight = [int16](Get-RandomWeight -Scale 50)
        $writer.Write($weight)

        if ($i % 100000 -eq 0) {
            $progress = [math]::Round(($i / $FeatureWeightsCount) * 100, 1)
            Write-Progress -Activity "Writing feature weights" -PercentComplete $progress
        }
    }
    Write-Progress -Activity "Writing feature weights" -Completed

    # Write feature biases (int32, but we'll convert to int16 equivalent)
    Write-Host "Writing feature biases..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $FeatureBiasCount; $i++) {
        $bias = [int32](Get-RandomWeight -Scale 1000)
        $writer.Write($bias)
    }

    # Write L1 weights (int16, but we'll convert to sbyte equivalent)
    Write-Host "Writing L1 weights..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $L1WeightsCount; $i++) {
        $weight = [int16](Get-RandomWeight -Scale 10)
        $writer.Write($weight)
    }

    # Write L1 biases (int32)
    Write-Host "Writing L1 biases..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $L1BiasCount; $i++) {
        $bias = [int32](Get-RandomWeight -Scale 500)
        $writer.Write($bias)
    }

    # Write L2 weights (int16)
    Write-Host "Writing L2 weights..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $L2WeightsCount; $i++) {
        $weight = [int16](Get-RandomWeight -Scale 20)
        $writer.Write($weight)
    }

    # Write L2 biases (int32)
    Write-Host "Writing L2 biases..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $L2BiasCount; $i++) {
        $bias = [int32](Get-RandomWeight -Scale 200)
        $writer.Write($bias)
    }

    # Write L3 weights (int16)
    Write-Host "Writing L3 weights..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $L3WeightsCount; $i++) {
        $weight = [int16](Get-RandomWeight -Scale 30)
        $writer.Write($weight)
    }

    # Write L3 biases (int32)
    Write-Host "Writing L3 biases..." -ForegroundColor Yellow
    for ($i = 0; $i -lt $L3BiasCount; $i++) {
        $bias = [int32](Get-RandomWeight -Scale 100)
        $writer.Write($bias)
    }

    # Close the writer
    $writer.Close()
    $fileStream.Close()

    # Get file info
    $fileInfo = Get-Item $TestNetworkPath
    $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)

    Write-Host "✅ Test NNUE network created successfully!" -ForegroundColor Green
    Write-Host "File: $TestNetworkPath" -ForegroundColor Cyan
    Write-Host "Size: $fileSizeMB MB" -ForegroundColor Cyan
    Write-Host "Bytes: $($fileInfo.Length)" -ForegroundColor Cyan

    # Replace the problematic obsidian.nnue with our test network
    if (Test-Path $obsidianPath) {
        Remove-Item $obsidianPath -Force
    }
    Copy-Item $TestNetworkPath $obsidianPath -Force
    Write-Host "✅ Replaced obsidian.nnue with test network" -ForegroundColor Green

    Write-Host "`n--- Network Details ---" -ForegroundColor Magenta
    Write-Host "Architecture: $KingBuckets buckets → $L1Size → $L2Size → $L3Size → $OutputSize" -ForegroundColor White
    Write-Host "Format: Compatible with Meridian NNUE implementation" -ForegroundColor White
    Write-Host "Weights: Randomized for testing (not trained)" -ForegroundColor White
    Write-Host "Purpose: Verify NNUE loading and evaluation functionality" -ForegroundColor White

    Write-Host "`n--- Next Steps ---" -ForegroundColor Yellow
    Write-Host "1. Run the engine to test NNUE loading" -ForegroundColor White
    Write-Host "2. Verify evaluation values are reasonable" -ForegroundColor White
    Write-Host "3. If working, consider training a real network" -ForegroundColor White
    Write-Host "4. To restore original: Copy obsidian_backup.nnue back" -ForegroundColor White

    exit 0

} catch {
    Write-Host "❌ Error creating test network: $($_.Exception.Message)" -ForegroundColor Red

    # Clean up on error
    if ($writer) { $writer.Close() }
    if ($fileStream) { $fileStream.Close() }
    if (Test-Path $TestNetworkPath) { Remove-Item $TestNetworkPath -Force }

    exit 1
}
