# PowerShell script to restore the original Meridian.exe

Write-Host "Restoring original Meridian.exe..." -ForegroundColor Green

$outputDir = "bin\Release\net9.0"
$originalExe = Join-Path $outputDir "Meridian.exe"
$backupExe = Join-Path $outputDir "Meridian_Original.exe"

if (Test-Path $backupExe) {
    Copy-Item -Path $backupExe -Destination $originalExe -Force
    Write-Host "Original Meridian.exe restored!" -ForegroundColor Green
} else {
    Write-Host "Error: Meridian_Original.exe not found!" -ForegroundColor Red
    Write-Host "You may need to rebuild the project." -ForegroundColor Yellow
}