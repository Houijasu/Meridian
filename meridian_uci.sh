#!/bin/bash
# UCI launcher script for Meridian chess engine

# Build the engine in release mode
dotnet build -c Release

# Run in UCI mode
dotnet run -c Release -- uci