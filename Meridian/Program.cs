namespace Meridian;

using System.Runtime.CompilerServices;

using Core;
using Protocols;

internal class Program
{
   [ModuleInitializer]
   public static void Initialize()
   {
      Console.Title = nameof(Meridian);
   }

   public static void Main(string[] args)
   {
      if (args.Length > 0 && args[0] == "perft")
      {
         Perft.RunPerftSuite();
         return;
      }
      
      // Check if we should run in UCI mode
      bool uciMode = args.Length > 0 && args[0].ToLowerInvariant() == "uci";
      
      var engine = new Engine();
      var search = new Search();
      
      if (uciMode)
      {
         // Run in UCI protocol mode
         var protocol = ProtocolFactory.Create("uci", engine, search);
         protocol.Run();
         return;
      }

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
      Console.WriteLine("To start in UCI mode: Meridian.exe uci");
      Console.WriteLine();

      engine.NewGame();
      engine.PrintBoard();

      while (true)
      {
         Console.Write("> ");
         string? input = Console.ReadLine();
         
         if (string.IsNullOrWhiteSpace(input))
            continue;

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
               protocol.Run();
               return;

            case "quit":
            case "exit":
               return;

            default:
               Console.WriteLine("Unknown command. Type 'help' for commands.");
               break;
         }
      }
   }
}
