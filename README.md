# Meridian Chess Engine

![Meridian Logo](https://via.placeholder.com/150)

**A high-performance chess engine written in C#.**

Meridian is a sophisticated chess engine designed for both competitive play and deep analysis. It implements the Universal Chess Interface (UCI) protocol, making it compatible with a wide range of popular chess GUIs.

## Features

- **UCI Compatible:** Works with any UCI-compliant graphical interface (e.g., Arena, Cute Chess, BanksiaGUI).
- **High-Performance Search:** Utilizes an advanced alpha-beta search algorithm with various optimizations for finding the best moves quickly.
- **Sophisticated Evaluation:** Employs a detailed evaluation function that considers material balance, piece-square tables, pawn structure, and other positional factors.
- **Transposition Table:** Caches previously analyzed positions to significantly speed up search times.
- **Comprehensive Test Suite:** Includes an extensive set of unit tests and performance benchmarks (Perft) to ensure correctness and efficiency.

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

### Building the Engine

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/your-username/Meridian.git
    cd Meridian
    ```

2.  **Build the solution:**
    ```bash
    dotnet build --configuration Release Meridian/Meridian.sln
    ```
    The compiled engine executable will be located at `Meridian/Meridian.CLI/bin/Release/net8.0/Meridian.CLI`.

## Usage

Meridian is a UCI engine, which means it doesn't have its own graphical interface. To use it, you'll need a UCI-compatible GUI.

### Example: Connecting to a GUI (like Arena)

1.  Open your preferred chess GUI.
2.  Navigate to the engine settings (e.g., "Engines" -> "Manage Engines" or similar).
3.  Add a new engine.
4.  When prompted, select the `Meridian.CLI` executable file.
5.  The GUI will then be able to communicate with Meridian to play games and analyze positions.

## Running Tests

To run the full suite of tests and ensure everything is working correctly:

```bash
dotnet test Meridian/Meridian.sln
```

## Contributing

Contributions are welcome! If you'd like to help improve Meridian, please feel free to:

-   Report bugs and suggest features by opening an issue.
-   Submit pull requests with your own enhancements.

When contributing, please ensure that all existing tests pass and, if necessary, add new tests for your changes.

## License

This project is licensed under the [MIT License](LICENSE).
