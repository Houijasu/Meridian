namespace Meridian;

using System.Runtime.CompilerServices;

using Core;
using Protocols;

internal class Program
{
   [ModuleInitializer]
   public static void Initialize()
   {
      // Don't set console title - it can cause issues with GUI integration
      // Console.Title = nameof(Meridian);
   }

   public static void Main(string[] args)
   {
      // Special case for perft testing
      if (args.Length > 0 && args[0] == "perft")
      {
         Perft.RunPerftSuite();
         return;
      }
      
      var engine = new Engine();
      var search = new Search();
      
      // Check for debug mode
      bool debugMode = args.Any(arg => arg.Equals("--debug", StringComparison.OrdinalIgnoreCase));
      
      // If explicitly started with 'uci' argument, go straight to UCI mode
      if (args.Length > 0 && args[0].ToLowerInvariant() == "uci")
      {
         var protocol = debugMode 
            ? new UciDebugProtocol(engine, search) 
            : (IProtocol)new UciProtocol(engine, search);
         protocol.Run();
         return;
      }
      
      // For GUI compatibility: when started with no arguments (or only --debug), don't print anything
      // Just wait for input and check if it's UCI
      if (args.Length == 0 || (args.Length == 1 && debugMode))
      {
         try
         {
            var firstInput = Console.ReadLine();
            if (firstInput == null) // EOF
               return;
               
            var trimmed = firstInput.Trim();
            if (trimmed.Equals("uci", StringComparison.OrdinalIgnoreCase) || 
                trimmed.Equals("UCI", StringComparison.Ordinal))
            {
               // Start UCI protocol and send initial response since we consumed the "uci" command
               var protocol = debugMode 
                  ? new UciDebugProtocol(engine, search) 
                  : (IProtocol)new UciProtocol(engine, search);
               protocol.Run(sendInitialUciResponse: true);
               return;
            }
            
            // Not UCI - continue in console mode
            ShowConsoleInterface(engine);
            
            // Process the first input if it wasn't UCI
            if (!string.IsNullOrWhiteSpace(firstInput))
            {
               if (!ProcessConsoleCommand(firstInput, engine, search))
                  return;
            }
         }
         catch
         {
            // If we can't read input, just exit silently
            return;
         }
      }
      else
      {
         // Console mode
         ShowConsoleInterface(engine);
      }

      // Console loop
      RunConsoleLoop(engine, search);
   }
   
   private static void ShowConsoleInterface(Engine engine)
   {
      Console.WriteLine("Meridian Chess Engine");
      Console.WriteLine("====================");
      Console.WriteLine("Commands:");
      Console.WriteLine("  new     - Start a new game");
      Console.WriteLine("  move    - Make a move (e.g., 'move e2e4')");
      Console.WriteLine("  go      - Engine makes a move");
      Console.WriteLine("  fen     - Set position from FEN");
      Console.WriteLine("  board   - Show current position");
      Console.WriteLine("  perft   - Run perft test suite");
      Console.WriteLine("  uci     - Switch to UCI protocol mode");
      Console.WriteLine("  quit    - Exit");
      Console.WriteLine();
      
      engine.NewGame();
      engine.PrintBoard();
   }
   
   private static void RunConsoleLoop(Engine engine, Search search)
   {
      while (true)
      {
         Console.Write("> ");
         string? input = Console.ReadLine();
         
         if (string.IsNullOrWhiteSpace(input))
            continue;
            
         if (!ProcessConsoleCommand(input, engine, search))
            break;
      }
   }
   
   private static bool ProcessConsoleCommand(string input, Engine engine, Search search)
   {
      string[] parts = input.Trim().Split(' ');
      string command = parts[0].ToLower();

         switch (command)
         {
            case "new":
               engine.NewGame();
               engine.PrintBoard();
               break;
 
            case "move":
               if (parts.Length < 2)
               {
                  Console.WriteLine("Usage: move <from><to>[promotion]");
                  Console.WriteLine("Example: move e2e4 or move e7e8q");
                  break;
               }
               if (engine.MakeMove(parts[1]))
               {
                  engine.PrintBoard();
               }
               else
               {
                  Console.WriteLine("Invalid move!");
               }
               break;

            case "go":
               Move bestMove = engine.Think(depthLimit: 6, timeLimitMs: 5000);
               if (bestMove.From != Square.None)
               {
                  Console.WriteLine($"Engine plays: {bestMove}");
                  engine.MakeMove(bestMove);
                  engine.PrintBoard();
               }
               else
               {
                  Console.WriteLine("No legal moves available!");
               }
               break;

            case "fen":
               if (parts.Length < 2)
               {
                  Console.WriteLine("Usage: fen <fen string>");
                  break;
               }
               try
               {
                  string fen = string.Join(" ", parts.Skip(1));
                  engine.SetPosition(fen);
                  engine.PrintBoard();
               }
               catch
               {
                  Console.WriteLine("Invalid FEN!");
               }
               break;

            case "board":
               engine.PrintBoard();
               break;

            case "perft":
               Perft.RunPerftSuite();
               break;
               
            case "uci":
               Console.WriteLine("Switching to UCI protocol mode...");
               var protocol = ProtocolFactory.Create("uci", engine, search);
               protocol?.Run();
               return false;

            case "quit":
            case "exit":
               return false;

            default:
               Console.WriteLine("Unknown command. Type 'help' for commands.");
               break;
         }
         
         return true; // Continue processing
   }
}
