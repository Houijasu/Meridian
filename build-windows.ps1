# PowerShell Build Script for Meridian Chess Engine
# Builds the project for Windows x64 as a single executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Debug,
    [switch]$NoTrim,
    [switch]$Test
)

# Set configuration based on Debug switch
if ($Debug) {
    $Configuration = "Debug"
}

Write-Host "Building Meridian Chess Engine for Windows..." -ForegroundColor Green
Write-Host ""

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Cyan
}
catch {
    Write-Host "Error: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Navigate to the Meridian solution directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "Meridian"

if (!(Test-Path $projectDir)) {
    Write-Host "Error: Could not find Meridian directory at $projectDir" -ForegroundColor Red
    exit 1
}

Set-Location $projectDir

# Create publish directory
$publishDir = "publish\$Runtime"
if (!(Test-Path "publish")) {
    New-Item -ItemType Directory -Path "publish" | Out-Null
}
if (!(Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean "Meridian\Meridian\Meridian.csproj" -c $Configuration | Out-Null

Write-Host "Building $Configuration version for $Runtime..." -ForegroundColor Yellow
Write-Host ""

# Build parameters
$buildParams = @(
    "publish"
    "Meridian\Meridian\Meridian.csproj"
    "-c", $Configuration
    "-r", $Runtime
    "--self-contained"
    "-p:PublishSingleFile=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "--output", ".\$publishDir"
)

# Add trimming if not disabled
if (!$NoTrim -and $Configuration -eq "Release") {
    $buildParams += "-p:PublishTrimmed=true"
}

# Execute build
$buildResult = & dotnet $buildParams

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed! Please check the error messages above." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Find the executable
$exePath = Get-ChildItem -Path $publishDir -Filter "*.exe" | Select-Object -First 1

if ($exePath) {
    Write-Host "Executable created: $($exePath.FullName)" -ForegroundColor Cyan
    Write-Host "File size: $([math]::Round($exePath.Length / 1MB, 2)) MB" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "You can now run the chess engine with:" -ForegroundColor Yellow
    Write-Host "  .\$publishDir\$($exePath.Name)" -ForegroundColor White
    Write-Host ""
    Write-Host "Or use it with a chess GUI like Arena, ChessBase, or Cute Chess." -ForegroundColor Yellow
    Write-Host ""

    # Optional: Run a quick test
    if ($Test) {
        Write-Host "Running quick UCI test..." -ForegroundColor Yellow
        Write-Host ""
        $testProcess = Start-Process -FilePath $exePath.FullName -ArgumentList "" -NoNewWindow -PassThru -RedirectStandardInput -RedirectStandardOutput
        $testProcess.StandardInput.WriteLine("uci")
        $testProcess.StandardInput.WriteLine("quit")
        $testProcess.WaitForExit(5000)
        if (!$testProcess.HasExited) {
            $testProcess.Kill()
        }
        Write-Host ""
    }
} else {
    Write-Host "Warning: Could not find executable in output directory" -ForegroundColor Yellow
}

# Display usage examples
Write-Host "Usage Examples:" -ForegroundColor Green
Write-Host "  Basic UCI test: echo 'uci' | .\$publishDir\Meridian.exe" -ForegroundColor White
Write-Host "  Position test:  echo 'position startpos' | .\$publishDir\Meridian.exe" -ForegroundColor White
Write-Host "  Search test:    echo 'position startpos' 'go depth 5' | .\$publishDir\Meridian.exe" -ForegroundColor White
Write-Host ""
Write-Host "Build script options:" -ForegroundColor Green
Write-Host "  -Debug          Build debug version" -ForegroundColor White
Write-Host "  -NoTrim         Disable trimming (larger executable)" -ForegroundColor White
Write-Host "  -Test           Run quick test after build" -ForegroundColor White
Write-Host "  -Runtime        Target runtime (default: win-x64)" -ForegroundColor White
Write-Host ""
