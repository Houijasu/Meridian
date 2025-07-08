@echo off
echo Testing NNUE Loading - Manual Commands
echo ======================================

echo.
echo 1. Checking network file...
if exist "networks\obsidian.nnue" (
    for %%i in (networks\obsidian.nnue) do echo Found: %%~zi bytes
) else (
    echo ERROR: networks\obsidian.nnue not found
    pause
    exit
)

echo.
echo 2. Building project...
cd Meridian
dotnet build Meridian.sln -c Release --verbosity minimal

echo.
echo 3. Testing NNUE loading...
echo uci | dotnet run --project Meridian\Meridian\Meridian.csproj

echo.
echo 4. Performance test...
(echo uci & echo position startpos & echo go depth 6 & echo quit) | dotnet run --project Meridian\Meridian\Meridian.csproj

echo.
echo Test complete! Look for:
echo - "SUCCESS: NNUE loaded" = Good
echo - "FAILED" or "ERROR" = Problem
echo - Node count: Low = NNUE working, High = Traditional evaluation
pause
