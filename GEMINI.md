# Meridian Chess Engine

## Project Overview

Meridian is a chess engine written in C#. It is designed for high-performance chess gameplay and analysis. The engine communicates using the Universal Chess Interface (UCI) protocol, allowing it to be used with compatible graphical user interfaces.

## Technology Stack

- **Language:** C#
- **Framework:** .NET

## Project Structure

The solution is organized into three main projects:

- `Meridian.Core`: Contains the core chess logic, including:
  - **Board Representation:** Manages the state of the chessboard.
  - **Move Generation:** Generates legal and pseudo-legal moves.
  - **Evaluation:** Analyzes board positions to determine which side has an advantage.
  - **Search:** Implements search algorithms (like Alpha-Beta) to find the best moves.
  - **Transposition Table:** Caches previously analyzed positions to speed up search.

- `Meridian.CLI`: A command-line interface for the engine, responsible for handling the UCI protocol.

- `Meridian.Tests`: A suite of unit and performance tests (including Perft) to ensure the engine's correctness and speed.

## How to Build

To build the project, you can use the .NET CLI:

```bash
dotnet build Meridian/Meridian.sln
```
