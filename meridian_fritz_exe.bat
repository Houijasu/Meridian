@echo off
REM Fritz-specific launcher for Meridian Chess Engine (compiled version)
REM Point this to your published executable

cd /d "%~dp0"
.\bin\Release\net9.0\win-x64\publish\Meridian.exe 2>nul