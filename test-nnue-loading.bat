@echo off
setlocal enabledelayedexpansion

echo ================================
echo  Meridian NNUE Loading Test
echo ================================
echo.

REM Navigate to Meridian directory
cd /d "%~dp0"
if not exist "Meridian\Meridian.sln" (
    echo Error: Cannot find Meridian.sln in Meridian directory
    pause
    exit /b 1
)

echo Step 1: Checking NNUE network file...
echo ====================================
if exist "networks\obsidian.nnue" (
    for %%i in (networks\obsidian.nnue) do (
        echo ✓ Found obsidian.nnue - Size: %%~zi bytes
        set /a file_size_mb=%%~zi/1024/1024
        echo   Size in MB: !file_size_mb! MB
        if !file_size_mb! LSS 20 (
            echo   ⚠ Warning: File seems small for NNUE network
        ) else (
            echo   ✓ File size looks reasonable
        )
    )
) else (
    echo ✗ NNUE network file not found at networks\obsidian.nnue
    echo   Please ensure the file exists before testing
    pause
    exit /b 1
)

echo.
echo Step 2: Building Meridian...
echo =============================
cd Meridian
dotnet clean Meridian.sln --verbosity quiet >nul 2>&1
echo Cleaning completed...

echo Building solution...
dotnet build Meridian.sln -c Release --verbosity quiet

if %errorlevel% neq 0 (
    echo ✗ Build failed! Check for compilation errors:
    dotnet build Meridian.sln -c Release
    pause
    exit /b 1
)

echo ✓ Build successful!

echo.
echo Step 3: Testing NNUE Loading...
echo ================================

REM Create test commands file
echo uci > temp_nnue_test.txt
echo quit >> temp_nnue_test.txt

echo Running engine startup test...
echo ==============================

REM Run the engine and capture output
dotnet run --project Meridian\Meridian\Meridian.csproj < temp_nnue_test.txt > nnue_test_output.txt 2>&1

echo Engine output:
echo --------------
type nnue_test_output.txt

echo.
echo Step 4: Analyzing Results...
echo =============================

REM Check for NNUE loading messages
findstr /C:"NNUE" nnue_test_output.txt > nul
if %errorlevel% equ 0 (
    echo ✓ NNUE messages found in output

    findstr /C:"SUCCESS: NNUE loaded" nnue_test_output.txt > nul
    if %errorlevel% equ 0 (
        echo ✓ NNUE loading was SUCCESSFUL!
        echo   Expected behavior: Engine should run at ~50-200 MN/s
    ) else (
        echo ⚠ NNUE loading attempt found, but success unclear

        findstr /C:"FAILED: Could not load NNUE" nnue_test_output.txt > nul
        if %errorlevel% equ 0 (
            echo ✗ NNUE loading FAILED
            echo   Engine will use traditional evaluation (~500+ MN/s)
        )

        findstr /C:"ERROR: NNUE file not found" nnue_test_output.txt > nul
        if %errorlevel% equ 0 (
            echo ✗ NNUE file not found during engine execution
        )
    )
) else (
    echo ⚠ No NNUE messages found - check for errors
)

echo.
echo Step 5: Performance Test...
echo ===========================

REM Create performance test commands
echo uci > temp_perf_test.txt
echo position startpos >> temp_perf_test.txt
echo go depth 6 >> temp_perf_test.txt
echo quit >> temp_perf_test.txt

echo Running quick depth 6 search to check speed...
echo -----------------------------------------------

dotnet run --project Meridian\Meridian\Meridian.csproj < temp_perf_test.txt > perf_test_output.txt 2>&1

REM Extract node count information
echo Performance results:
findstr /C:"MN" perf_test_output.txt
findstr /C:"kN" perf_test_output.txt

REM Look for high node counts that indicate traditional evaluation
findstr /C:"00MN" perf_test_output.txt > nul
if %errorlevel% equ 0 (
    echo.
    echo Analysis: HIGH node count detected (>100 MN/s)
    echo → This suggests TRADITIONAL evaluation is running
    echo → NNUE is likely NOT active
) else (
    findstr /C:"MN" perf_test_output.txt > nul
    if %errorlevel% equ 0 (
        echo.
        echo Analysis: Moderate node count detected
        echo → This might indicate NNUE is working
        echo → Check specific numbers above
    )
)

echo.
echo Step 6: Summary...
echo ==================

findstr /C:"Final evaluation mode" nnue_test_output.txt > nul
if %errorlevel% equ 0 (
    echo Final evaluation mode:
    findstr /C:"Final evaluation mode" nnue_test_output.txt
) else (
    echo Could not determine final evaluation mode
)

echo.
echo Debug files created:
echo - nnue_test_output.txt (engine startup log)
echo - perf_test_output.txt (performance test log)
echo.

REM Cleanup
del temp_nnue_test.txt 2>nul
del temp_perf_test.txt 2>nul

echo Test completed!
echo.
echo Expected results if NNUE works:
echo - "SUCCESS: NNUE loaded" message
echo - "Final evaluation mode: NNUE"
echo - Node count around 50-200 MN/s (not 500+ MN/s)
echo.
echo If NNUE is not working:
echo - "FAILED" or "ERROR" messages
echo - "Final evaluation mode: TRADITIONAL"
echo - Node count 500+ MN/s
echo.

pause
