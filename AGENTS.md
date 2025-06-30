# AGENTS.md

## Build, Lint, and Test Commands
- **Build the project:** `dotnet build -c Release Meridian/Meridian.sln`
- **Run a single test:** Use the provided shell scripts:
  - Quick test: `./quick_test.sh`
  - Fixed test: `./test_fixed.sh`
  - Deep search test: `./test_deep_search.sh`
  - Fix test: `./test_fix.sh`

## Code Style Guidelines
- **Imports:** Group standard library imports first, followed by third-party libraries, and then local imports.
- **Formatting:** Use 4 spaces for indentation. Ensure consistent line length (max 80 characters).
- **Types:** Prefer explicit types over var for clarity.
- **Naming Conventions:** Use PascalCase for public types and methods, camelCase for private members and parameters.
- **Error Handling:** Use exceptions for error handling; avoid returning null. Always log errors with context.

## Cursor and Copilot Rules
- Ensure all code adheres to the Cursor rules defined in `.cursor/rules/`.
- Follow Copilot instructions as specified in `.github/copilot-instructions.md`.