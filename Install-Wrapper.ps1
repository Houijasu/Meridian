# PowerShell script to set up the Meridian wrapper for debugging

Write-Host "Setting up Meridian wrapper for debugging..." -ForegroundColor Green

# Build the wrapper
Write-Host "Building wrapper..." -ForegroundColor Yellow
Push-Location MeridianWrapper
dotnet build -c Release
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Get the output directory
$outputDir = "bin\Release\net9.0"

# Rename original Meridian.exe if it exists
$originalExe = Join-Path $outputDir "Meridian.exe"
$backupExe = Join-Path $outputDir "Meridian_Original.exe"
$wrapperExe = Join-Path "MeridianWrapper" $outputDir "MeridianWrapper.exe"

if (Test-Path $originalExe) {
    Write-Host "Backing up original Meridian.exe..." -ForegroundColor Yellow
    Move-Item -Path $originalExe -Destination $backupExe -Force
}

# Copy wrapper as Meridian.exe
Write-Host "Installing wrapper as Meridian.exe..." -ForegroundColor Yellow
Copy-Item -Path $wrapperExe -Destination $originalExe -Force

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "`nThe wrapper is now installed as Meridian.exe" -ForegroundColor Cyan
Write-Host "When Fritz runs the engine, all communication will be logged to:" -ForegroundColor Cyan
Write-Host "  $outputDir\meridian_debug_*.log" -ForegroundColor White
Write-Host "`nTo restore the original engine, run Restore-Original.ps1" -ForegroundColor Yellow