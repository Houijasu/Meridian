# Binary File Analyzer for obsidian.nnue
# This script analyzes the binary structure of the NNUE network file

param(
    [string]$FilePath = "networks\obsidian.nnue"
)

Write-Host "NNUE Binary File Analyzer" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green
Write-Host ""

if (!(Test-Path $FilePath)) {
    Write-Host "Error: File not found: $FilePath" -ForegroundColor Red
    exit 1
}

$fileInfo = Get-Item $FilePath
$fileSize = $fileInfo.Length
Write-Host "File: $FilePath" -ForegroundColor Cyan
Write-Host "Size: $fileSize bytes ($([math]::Round($fileSize / 1MB, 2)) MB)" -ForegroundColor Cyan
Write-Host ""

# Read file as bytes
$bytes = [System.IO.File]::ReadAllBytes($FilePath)

Write-Host "=== HEADER ANALYSIS ===" -ForegroundColor Yellow
Write-Host ""

# Check first 256 bytes for patterns
Write-Host "First 64 bytes (hex):" -ForegroundColor White
for ($i = 0; $i -lt 64; $i += 16) {
    $hexLine = ""
    $asciiLine = ""
    for ($j = 0; $j -lt 16 -and ($i + $j) -lt $bytes.Length; $j++) {
        $byte = $bytes[$i + $j]
        $hexLine += "{0:X2} " -f $byte
        if ($byte -ge 32 -and $byte -le 126) {
            $asciiLine += [char]$byte
        } else {
            $asciiLine += "."
        }
    }
    Write-Host ("{0:X4}: {1,-48} {2}" -f $i, $hexLine, $asciiLine)
}

Write-Host ""
Write-Host "=== DATA TYPE ANALYSIS ===" -ForegroundColor Yellow
Write-Host ""

# Test different data interpretations at various offsets
$testOffsets = @(0, 64, 128, 256, 512, 1024, 2048, 4096)

foreach ($offset in $testOffsets) {
    if ($offset + 40 -gt $bytes.Length) { continue }

    Write-Host "Offset $offset (0x{0:X}):" -f $offset -ForegroundColor Cyan

    # Test as int16 (2 bytes)
    $int16Values = @()
    $nonZeroInt16 = 0
    for ($i = 0; $i -lt 10; $i++) {
        if ($offset + $i * 2 + 1 -lt $bytes.Length) {
            $value = [BitConverter]::ToInt16($bytes, $offset + $i * 2)
            $int16Values += $value
            if ($value -ne 0) { $nonZeroInt16++ }
        }
    }
    Write-Host "  Int16: [$($int16Values -join ', ')] (${nonZeroInt16}/10 non-zero)" -ForegroundColor White

    # Test as float (4 bytes)
    $floatValues = @()
    $reasonableFloats = 0
    for ($i = 0; $i -lt 10; $i++) {
        if ($offset + $i * 4 + 3 -lt $bytes.Length) {
            $value = [BitConverter]::ToSingle($bytes, $offset + $i * 4)
            $floatValues += "{0:F3}" -f $value
            if ([Math]::Abs($value) -gt 0.001 -and [Math]::Abs($value) -lt 10.0 -and ![Single]::IsNaN($value) -and ![Single]::IsInfinity($value)) {
                $reasonableFloats++
            }
        }
    }
    Write-Host "  Float: [$($floatValues -join ', ')] (${reasonableFloats}/10 reasonable)" -ForegroundColor White

    # Test as int32 (4 bytes)
    $int32Values = @()
    $nonZeroInt32 = 0
    for ($i = 0; $i -lt 10; $i++) {
        if ($offset + $i * 4 + 3 -lt $bytes.Length) {
            $value = [BitConverter]::ToInt32($bytes, $offset + $i * 4)
            $int32Values += $value
            if ($value -ne 0) { $nonZeroInt32++ }
        }
    }
    Write-Host "  Int32: [$($int32Values -join ', ')] (${nonZeroInt32}/10 non-zero)" -ForegroundColor White

    Write-Host ""
}

Write-Host "=== PATTERN ANALYSIS ===" -ForegroundColor Yellow
Write-Host ""

# Look for patterns in the file
$zeroRuns = @()
$nonZeroRuns = @()
$currentRun = @{ Type = ""; Start = 0; Length = 0 }

for ($i = 0; $i -lt $bytes.Length; $i++) {
    $isZero = $bytes[$i] -eq 0

    if ($currentRun.Type -eq "" -or ($currentRun.Type -eq "zero" -and !$isZero) -or ($currentRun.Type -eq "nonzero" -and $isZero)) {
        # End current run, start new one
        if ($currentRun.Type -ne "" -and $currentRun.Length -gt 0) {
            if ($currentRun.Type -eq "zero") {
                $zeroRuns += @{ Start = $currentRun.Start; Length = $currentRun.Length }
            } else {
                $nonZeroRuns += @{ Start = $currentRun.Start; Length = $currentRun.Length }
            }
        }

        $currentRun = @{
            Type = if ($isZero) { "zero" } else { "nonzero" }
            Start = $i
            Length = 1
        }
    } else {
        $currentRun.Length++
    }
}

# Add final run
if ($currentRun.Type -ne "" -and $currentRun.Length -gt 0) {
    if ($currentRun.Type -eq "zero") {
        $zeroRuns += @{ Start = $currentRun.Start; Length = $currentRun.Length }
    } else {
        $nonZeroRuns += @{ Start = $currentRun.Start; Length = $currentRun.Length }
    }
}

Write-Host "Zero byte runs (first 10):" -ForegroundColor White
$zeroRuns | Select-Object -First 10 | ForEach-Object {
    Write-Host "  Offset {0:X} - {1:X} ({2} bytes)" -f $_.Start, ($_.Start + $_.Length - 1), $_.Length
}

Write-Host ""
Write-Host "Non-zero byte runs (first 10):" -ForegroundColor White
$nonZeroRuns | Select-Object -First 10 | ForEach-Object {
    Write-Host "  Offset {0:X} - {1:X} ({2} bytes)" -f $_.Start, ($_.Start + $_.Length - 1), $_.Length
}

Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Yellow
Write-Host ""

$totalZeroBytes = ($zeroRuns | Measure-Object -Property Length -Sum).Sum
$totalNonZeroBytes = ($nonZeroRuns | Measure-Object -Property Length -Sum).Sum
$zeroPercentage = [math]::Round(($totalZeroBytes / $fileSize) * 100, 2)

Write-Host "Zero bytes: $totalZeroBytes / $fileSize (${zeroPercentage}%)" -ForegroundColor White
Write-Host "Non-zero bytes: $totalNonZeroBytes / $fileSize" -ForegroundColor White
Write-Host "Total runs: $($zeroRuns.Count + $nonZeroRuns.Count)" -ForegroundColor White

if ($zeroPercentage -gt 90) {
    Write-Host ""
    Write-Host "WARNING: File is mostly zeros (${zeroPercentage}%)!" -ForegroundColor Red
    Write-Host "This suggests the file may be:" -ForegroundColor Yellow
    Write-Host "- Corrupted or incomplete" -ForegroundColor White
    Write-Host "- Compressed (but not a standard format)" -ForegroundColor White
    Write-Host "- Using a sparse format we don't understand" -ForegroundColor White
} elseif ($nonZeroRuns.Count -gt 0) {
    Write-Host ""
    Write-Host "File contains non-zero data. Largest non-zero sections:" -ForegroundColor Green
    $nonZeroRuns | Sort-Object -Property Length -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Host "  Offset {0:X} ({1} bytes)" -f $_.Start, $_.Length -ForegroundColor White
    }
}

Write-Host ""
Write-Host "=== RECOMMENDATIONS ===" -ForegroundColor Yellow
Write-Host ""

if ($zeroPercentage -gt 50) {
    Write-Host "1. Try downloading a fresh copy of obsidian.nnue" -ForegroundColor White
    Write-Host "2. Verify the file is the correct Obsidian network file" -ForegroundColor White
    Write-Host "3. Check if the file needs decompression" -ForegroundColor White
} else {
    Write-Host "1. File appears to have valid data" -ForegroundColor White
    Write-Host "2. Try reading from the largest non-zero sections" -ForegroundColor White
    Write-Host "3. Experiment with different data type interpretations" -ForegroundColor White
}

Write-Host ""
Write-Host "Analysis complete!" -ForegroundColor Green
