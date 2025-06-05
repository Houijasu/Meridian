@echo off
REM Fritz-specific launcher for Meridian Chess Engine
REM Ensures no output before UCI command

cd /d "%~dp0"
dotnet run -c Release --no-build 2>nul