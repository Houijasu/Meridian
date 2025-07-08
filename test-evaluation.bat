@echo off
setlocal enabledelayedexpansion

echo ================================
echo  Meridian Chess Engine Test
echo ================================
echo.

REM Navigate to Meridian directory
cd /d "%~dp0"
if not exist "Meridian\Meridian.sln" (
    echo Error: Cannot find Meridian.sln in Meridian directory
    pause
    exit /b 1
)

echo Building Meridian...
cd Meridian
dotnet build Meridian.sln -c Release --verbosity quiet

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.

echo Testing engine with starting position...
echo.

REM Create temporary UCI commands file
echo uci > temp_commands.txt
echo position startpos >> temp_commands.txt
echo go depth 8 >> temp_commands.txt
echo quit >> temp_commands.txt

echo Running engine test...
echo ========================

REM Run the engine with test commands
dotnet run --project Meridian\Meridian\Meridian.csproj < temp_commands.txt

echo ========================
echo.

REM Clean up
del temp_commands.txt 2>nul

echo Test completed!
echo.
echo Expected to see:
echo - "NNUE disabled - using traditional evaluation"
echo - Better opening moves like e4, d4, Nf3, c4 (NOT a3, h3, Nh3)
echo - Evaluation scores that vary (not all 0.00)
echo.

pause
