@echo off
setlocal enabledelayedexpansion

echo Building Meridian Chess Engine for Windows...
echo.

REM Check if .NET is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET SDK is not installed or not in PATH
    echo Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Navigate to the Meridian solution directory
cd /d "%~dp0Meridian"
if %errorlevel% neq 0 (
    echo Error: Could not navigate to Meridian directory
    pause
    exit /b 1
)

REM Create publish directory
if not exist "publish" mkdir "publish"
if not exist "publish\win-x64" mkdir "publish\win-x64"

echo Cleaning previous builds...
dotnet clean Meridian/Meridian/Meridian.csproj -c Release >nul 2>&1

echo Building Release version for Windows x64...
echo.

REM First build without single file to include NNUE network
dotnet publish Meridian/Meridian/Meridian.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained ^
    -p:PublishSingleFile=false ^
    --output "./publish/win-x64-temp"

REM Copy NNUE network file to output directory
if exist "networks\obsidian.nnue" (
    if not exist "publish\win-x64-temp\networks" mkdir "publish\win-x64-temp\networks"
    copy "networks\obsidian.nnue" "publish\win-x64-temp\networks\obsidian.nnue"
    echo NNUE network file copied successfully
) else (
    echo Warning: NNUE network file not found at networks\obsidian.nnue
)

REM Now build single executable with NNUE included
dotnet publish Meridian/Meridian/Meridian.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained ^
    -p:PublishSingleFile=true ^
    -p:PublishTrimmed=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    --output "./publish/win-x64"

if %errorlevel% neq 0 (
    echo.
    echo Build failed! Please check the error messages above.
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo.
echo Output files:
dir "publish\win-x64\*.exe" /b
echo.
echo Executable location: %cd%\publish\win-x64\Meridian.exe
echo.
echo Manual NNUE setup (if needed):
if exist "networks\obsidian.nnue" (
    if not exist "publish\win-x64\networks" mkdir "publish\win-x64\networks"
    copy "networks\obsidian.nnue" "publish\win-x64\networks\obsidian.nnue" >nul
    echo NNUE network copied to executable directory
)
echo.
echo You can now run the chess engine with:
echo   .\publish\win-x64\Meridian.exe
echo.
echo Or use it with a chess GUI like Arena, ChessBase, or Cute Chess.
echo.
echo Important: Make sure the networks folder with obsidian.nnue is in the same directory as the executable
echo.

REM Optional: Run a quick test
set /p test="Run a quick test? (y/n): "
if /i "%test%"=="y" (
    echo.
    echo Running quick UCI test...
    pushd "publish\win-x64"
    echo uci | Meridian.exe
    popd
    echo.
)

pause
