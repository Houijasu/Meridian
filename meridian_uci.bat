@echo off
REM UCI launcher script for Meridian chess engine

REM Build the engine in release mode
dotnet build -c Release

REM Run in UCI mode
dotnet run -c Release -- uci