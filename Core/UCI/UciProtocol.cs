namespace Meridian.Core.UCI;

using System.Collections.Concurrent;
using System.Text;

using MoveGeneration;

using Search;

/// <summary>
///    Implements the Universal Chess Interface (UCI) protocol.
/// </summary>
public class UciProtocol
{
    /// <summary>
    ///    Engine name.
    /// </summary>
    public const string EngineName = "Meridian";
    
    /// <summary>
    ///    Engine author.
    /// </summary>
    public const string EngineAuthor = "Houijasu";
    
    /// <summary>
    ///    Engine version.
    /// </summary>
    public const string EngineVersion = "0.0.1";

   private Position currentPosition = Position.StartingPosition();
   private int hashSizeMB = 128;
   private int numThreads = 1;
   private MultiThreadedSearchEngine searchEngine = new();
   private CancellationTokenSource? searchCts;
   private Task? searchTask;
   private readonly ConcurrentQueue<string> commandQueue = new();
   private volatile bool shouldQuit;
   private readonly object searchLock = new();
   private volatile bool suppressBestMove;
   private readonly object disposeLock = new();
   private volatile bool isDisposing;
   private readonly AutoResetEvent commandAvailable = new(false);
   
   // Pondering state
   private volatile bool isPondering;
   private volatile int ponderTimeRemaining;


   /// <summary>
   ///    Starts the UCI protocol loop.
   /// </summary>
   public void Run()
   {
      // Start the input reader thread
      var inputThread = new Thread(InputReaderThread)
      {
         IsBackground = true,
         Name = "UCI Input Reader"
      };
      inputThread.Start();
      
      // Main command processing loop
      while (!shouldQuit)
      {
         // Wait for a command or timeout after 100ms to check shouldQuit
         if (commandAvailable.WaitOne(100))
         {
            // Process all pending commands
            while (commandQueue.TryDequeue(out var command))
            {
               if (command == "quit")
               {
                  shouldQuit = true;
                  StopSearchAndCleanup(suppressOutput: true);
                  break;
               }
               
               ProcessCommand(command);
            }
         }
      }
      
      // Wait for input thread to finish
      inputThread.Join(1000);
      
      // Dispose of the event
      commandAvailable.Dispose();
   }
   
   /// <summary>
   ///    Reads input from stdin in a separate thread.
   /// </summary>
   private void InputReaderThread()
   {
      try
      {
         string? input;
         while ((input = Console.ReadLine()) != null && !shouldQuit)
         {
            commandQueue.Enqueue(input);
            commandAvailable.Set(); // Signal that a command is available
            if (input == "quit")
               break;
         }
      }
      catch (Exception ex)
      {
         Console.Error.WriteLine($"Error reading input: {ex.Message}");
      }
   }

   /// <summary>
   ///    Processes a single UCI command.
   /// </summary>
   private void ProcessCommand(string command)
   {
      if (string.IsNullOrWhiteSpace(command)) return;

      // Use span-based splitting to avoid allocations
      var commandSpan = command.AsSpan();

      // Quick check for single-word commands
      var firstSpace = command.IndexOf(' ');

      if (firstSpace == -1)
         switch (command)
         {
            case "uci":
               HandleUci();
               return;

            case "isready":
               HandleIsReady();
               return;

            case "ucinewgame":
               HandleNewGame();
               return;

            case "stop":
               HandleStop();
               return;
               
            case "ponderhit":
               HandlePonderHit();
               return;

            case "quit":
               return;

            // Non-standard commands commented out for Fritz compatibility
            // case "d":
            // case "display":
            //    DisplayPosition();
            //    return;

            // Remove help command - not part of UCI standard
            // case "help":
            //    ShowHelp();
            //    return;
         }

      // For multi-word commands, we need to split
      var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (tokens.Length == 0) return;

      switch (tokens[0])
      {
         case "uci":
            HandleUci();
            break;

         case "isready":
            HandleIsReady();
            break;

         case "ucinewgame":
            HandleNewGame();
            break;

         case "position":
            HandlePosition(tokens);
            break;

         case "go":
            HandleGo(tokens);
            break;

         case "stop":
            HandleStop();
            break;
            
         case "ponderhit":
            HandlePonderHit();
            break;

         case "setoption":
            HandleSetOption(tokens);
            break;

         case "debug":
            // Debug mode on/off - ignored for now
            break;

         // Non-standard commands (for debugging)
         case "eval":
            HandleEval();
            break;
            
         // case "d":
         // case "display":
         //    DisplayPosition();
         //    break;

         // Remove help command - not part of UCI standard  
         // case "help":
         //    ShowHelp();
         //    break;
      }
   }

   /// <summary>
   ///    Handles the UCI initialization command.
   /// </summary>
   private void HandleUci()
   {
      // Fritz is very strict about UCI format - keep it simple
      Console.WriteLine($"id name {EngineName} {EngineVersion}");
      Console.WriteLine($"id author {EngineAuthor}");
      
      // Engine options
      Console.WriteLine("option name Hash type spin default 128 min 1 max 16384");
      Console.WriteLine($"option name Threads type spin default 1 min 1 max {Environment.ProcessorCount}");
      Console.WriteLine("option name Ponder type check default false");
      
      Console.WriteLine("uciok");
   }

   /// <summary>
   ///    Handles the new game command.
   /// </summary>
   private void HandleNewGame()
   {
      StopSearchAndCleanup(suppressOutput: true); // Stop any ongoing search
      currentPosition = Position.StartingPosition();
      searchEngine.ClearTT();
      searchEngine.ClearMoveOrdering();
      isPondering = false;
   }

   /// <summary>
   ///    Handles the position command.
   /// </summary>
   private void HandlePosition(string[] tokens)
   {
      // Stop any ongoing search and wait for it to complete
      StopSearchAndCleanup(suppressOutput: true);
      
      var index = 1;

      if (index >= tokens.Length) return;

      // Parse starting position
      if (tokens[index] == "startpos")
      {
         currentPosition = Position.StartingPosition();
         index++;
      } else if (tokens[index] == "fen")
      {
         index++;
         var fenBuilder = new StringBuilder();

         // Build FEN string
         while (index < tokens.Length && tokens[index] != "moves")
         {
            if (fenBuilder.Length > 0) fenBuilder.Append(' ');
            fenBuilder.Append(tokens[index]);
            index++;
         }

         try
         {
            currentPosition = Fen.Parse(fenBuilder.ToString());
         }
         catch (Exception)
         {
            // Silently ignore FEN parsing errors for Fritz compatibility
            return;
         }
      }

      // Parse moves
      if (index < tokens.Length && tokens[index] == "moves")
      {
         index++;

         while (index < tokens.Length)
         {
            var move = ParseMove(tokens[index]);

            if (move != Move.Null)
            {
               currentPosition.MakeMove(move);
            } else
            {
               // Silently ignore invalid moves for Fritz compatibility
               break;
            }

            index++;
         }
      }
      
      // Reset suppress flag after position is updated
      suppressBestMove = false;
      
   }

   /// <summary>
   ///    Handles the go command.
   /// </summary>
   private void HandleGo(string[] tokens)
   {
      // Parse time controls
      var depth = SearchConstants.MaxDepth;
      var moveTime = int.MaxValue;
      var whiteTime = 0;
      var blackTime = 0;
      var whiteInc = 0;
      var blackInc = 0;
      var movesToGo = 40; // Default moves to time control
      var infinite = false;
      var ponder = false;

      for (var i = 1; i < tokens.Length; i++)
      {
         switch (tokens[i])
         {
            case "depth":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var d))
               {
                  depth = d;
                  i++;
               }

               break;

            case "movetime":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var mt))
               {
                  moveTime = mt;
                  i++;
               }

               break;

            case "wtime":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var wt))
               {
                  whiteTime = wt;
                  i++;
               }

               break;

            case "btime":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var bt))
               {
                  blackTime = bt;
                  i++;
               }

               break;

            case "winc":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var wi))
               {
                  whiteInc = wi;
                  i++;
               }

               break;

            case "binc":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var bi))
               {
                  blackInc = bi;
                  i++;
               }

               break;

            case "movestogo":
               if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var mtg))
               {
                  movesToGo = mtg;
                  i++;
               }

               break;

            case "infinite":
               infinite = true;
               break;
               
            case "ponder":
               ponder = true;
               break;
         }
      }

      // Calculate time for this move
      if (!infinite && moveTime == int.MaxValue)
      {
         var ourTime = currentPosition.SideToMove == Color.White
            ? whiteTime
            : blackTime;

         var ourInc = currentPosition.SideToMove == Color.White
            ? whiteInc
            : blackInc;

         if (ourTime > 0)
         {
            // Simple time management: use 1/movesToGo of remaining time plus increment
            moveTime = ourTime / movesToGo + ourInc * 3 / 4;

            // Safety margin
            moveTime = Math.Min(moveTime, ourTime - 50);
            moveTime = Math.Max(moveTime, 1);
         }
      }

      // Cancel any ongoing search first
      StopSearchAndCleanup(suppressOutput: false);
      
      // Start new search
      if (ponder)
      {
         isPondering = true;
         ponderTimeRemaining = moveTime;
         StartNewSearch(currentPosition, depth, int.MaxValue, true); // Ponder with infinite time until ponderhit
      }
      else
      {
         isPondering = false;
         StartNewSearch(currentPosition, depth, moveTime, infinite);
      }
   }

   /// <summary>
   ///    Handles the setoption command.
   /// </summary>
   private void HandleSetOption(string[] tokens)
   {
      // Parse: setoption name <name> value <value>
      if (tokens.Length < 5) return;

      var nameIndex = Array.IndexOf(tokens, "name");
      var valueIndex = Array.IndexOf(tokens, "value");

      if (nameIndex == -1 || valueIndex == -1 || valueIndex <= nameIndex) return;

      // Build option name (might be multiple words)
      var optionName = string.Join(" ", tokens.Skip(nameIndex + 1).Take(valueIndex - nameIndex - 1));
      var optionValue = string.Join(" ", tokens.Skip(valueIndex + 1));

      switch (optionName.ToLower())
      {
         case "hash":
            if (int.TryParse(optionValue, out var hashSize))
            {
               hashSize = Math.Clamp(hashSize, 1, 16384);

               if (hashSize != hashSizeMB)
               {
                  hashSizeMB = hashSize;
                  searchEngine = new MultiThreadedSearchEngine(hashSizeMB, numThreads);
               }
            }

            break;

         case "threads":
            if (int.TryParse(optionValue, out var threads))
            {
               threads = Math.Clamp(threads, 1, Environment.ProcessorCount);
               
               if (threads != numThreads)
               {
                  numThreads = threads;
                  searchEngine = new MultiThreadedSearchEngine(hashSizeMB, numThreads);
               }
            }
            break;

         case "ponder":
            // Ponder option is accepted for UCI compatibility but not currently used
            // The engine declares support for pondering but doesn't implement it yet
            _ = bool.TryParse(optionValue, out _);
            break;
      }
   }


   /// <summary>
   ///    Displays the current position (non-standard debug command).
   /// </summary>
   private void DisplayPosition()
   {
      Console.WriteLine();
      Console.WriteLine(currentPosition.ToString());
      Console.WriteLine($"FEN: {Fen.ToFen(in currentPosition)}");
      Console.WriteLine($"Side to move: {currentPosition.SideToMove}");
      Console.WriteLine($"Castling: {currentPosition.CastlingRights}");
      Console.WriteLine($"En passant: {currentPosition.EnPassantSquare}");
      Console.WriteLine($"Halfmove clock: {currentPosition.HalfmoveClock}");
      Console.WriteLine($"Fullmove: {currentPosition.FullmoveNumber}");
   }

   /// <summary>
   ///    Handles the stop command.
   /// </summary>
   private void HandleStop()
   {
      suppressBestMove = false; // GUI explicitly wants bestmove
      isPondering = false;
      StopSearchAndCleanup(suppressOutput: false);
   }
   
   /// <summary>
   ///    Handles the ponderhit command.
   /// </summary>
   private void HandlePonderHit()
   {
      if (!isPondering)
         return;
         
      isPondering = false;
      
      // Continue the search with the remaining time
      // The search is already running, we just need to set a time limit
      lock (searchLock)
      {
         if (searchTask is { IsCompleted: false } && searchCts != null)
         {
            // Create a new timer task to stop the search after the remaining time
            Task.Run(async () =>
            {
               await Task.Delay(ponderTimeRemaining);
               searchEngine.StopSearch();
            });
         }
      }
   }
   
   /// <summary>
   ///    Handles the eval command (non-standard debug command).
   /// </summary>
   private void HandleEval()
   {
      var score = Evaluation.Evaluator.Evaluate(in currentPosition);
      var absoluteScore = Evaluation.Evaluator.EvaluateAbsolute(in currentPosition);
      
      Console.WriteLine($"Evaluation: {score} cp (from side to move)");
      Console.WriteLine($"Absolute evaluation: {absoluteScore} cp (positive = white advantage)");
      Console.WriteLine();
      
      // Show evaluation breakdown
      var materialScore = 0;
      materialScore += Bitboard.PopCount(currentPosition.WhitePawns) * 100;
      materialScore += Bitboard.PopCount(currentPosition.WhiteKnights) * 320;
      materialScore += Bitboard.PopCount(currentPosition.WhiteBishops) * 330;
      materialScore += Bitboard.PopCount(currentPosition.WhiteRooks) * 500;
      materialScore += Bitboard.PopCount(currentPosition.WhiteQueens) * 900;
      
      materialScore -= Bitboard.PopCount(currentPosition.BlackPawns) * 100;
      materialScore -= Bitboard.PopCount(currentPosition.BlackKnights) * 320;
      materialScore -= Bitboard.PopCount(currentPosition.BlackBishops) * 330;
      materialScore -= Bitboard.PopCount(currentPosition.BlackRooks) * 500;
      materialScore -= Bitboard.PopCount(currentPosition.BlackQueens) * 900;
      
      Console.WriteLine($"Material balance: {materialScore} cp");
      
      var pawnScore = Evaluation.PawnStructure.Evaluate(in currentPosition);
      Console.WriteLine($"Pawn structure: {pawnScore} cp");
      
      var kingSafetyScore = Evaluation.KingSafety.Evaluate(in currentPosition);
      Console.WriteLine($"King safety: {kingSafetyScore} cp");
      
      var mobilityScore = Evaluation.Mobility.Evaluate(in currentPosition);
      Console.WriteLine($"Mobility: {mobilityScore} cp");
      
      if (Evaluation.Endgame.IsEndgame(in currentPosition))
      {
         var endgameScore = Evaluation.Endgame.Evaluate(in currentPosition);
         Console.WriteLine($"Endgame evaluation: {endgameScore} cp");
      }
      
      var phase = Evaluation.Endgame.GetEndgamePhase(in currentPosition);
      Console.WriteLine($"Game phase: {phase}/256 endgame ({256-phase}/256 middlegame)");
   }

   /// <summary>
   ///    Parses a move in algebraic notation.
   /// </summary>
   private Move ParseMove(string moveStr)
   {
      if (moveStr.Length < 4) return Move.Null;

      // Parse source and destination squares
      var from = ParseSquare(moveStr[..2]);
      var to = ParseSquare(moveStr[2..4]);

      if (from == Square.None || to == Square.None) return Move.Null;

      // Generate legal moves to find the matching move
      Span<Move> moveBuffer = stackalloc Move[256];
      var moveList = new MoveList(moveBuffer);
      MoveGenerator.GenerateMoves(in currentPosition, ref moveList);

      // Find the move that matches
      for (var i = 0; i < moveList.Count; i++)
      {
         var move = moveList.Moves[i];

         if (move.From == from && move.To == to)
         {
            // Check for promotion
            if (moveStr.Length > 4 && move.IsPromotion)
            {
               var promoPiece = char.ToLower(moveStr[4]);
               var promoType = move.GetPromotionType();

               var matches = promoPiece switch {
                  'q' => promoType == PieceType.Queen,
                  'r' => promoType == PieceType.Rook,
                  'b' => promoType == PieceType.Bishop,
                  'n' => promoType == PieceType.Knight,
                  _ => false
               };

               if (matches) return move;
            } else if (moveStr.Length == 4 && !move.IsPromotion)
               return move;
         }
      }

      return Move.Null;
   }

   /// <summary>
   ///    Parses a square from algebraic notation.
   /// </summary>
   private static Square ParseSquare(string squareStr)
   {
      if (squareStr.Length != 2) return Square.None;

      var file = squareStr[0] - 'a';
      var rank = squareStr[1] - '1';

      if (file < 0 || file > 7 || rank < 0 || rank > 7)
         return Square.None;

      return (Square)(rank * 8 + file);
   }

   /// <summary>
   ///    Handles the isready command.
   /// </summary>
   private void HandleIsReady()
   {
      // Wait for any ongoing operations to complete
      lock (searchLock)
      {
         if (searchTask is { IsCompleted: false })
         {
            try
            {
               // Give more time for cleanup to complete
               if (!searchTask.Wait(500))
               {
                  Console.Error.WriteLine("Warning: Search task still running during isready");
               }
            }
            catch (AggregateException ae)
            {
               // Log non-cancellation exceptions
               foreach (var ex in ae.InnerExceptions)
               {
                  if (!(ex is OperationCanceledException))
                  {
                     Console.Error.WriteLine($"IsReady error: {ex.Message}");
                  }
               }
            }
            catch (Exception ex)
            {
               Console.Error.WriteLine($"IsReady error: {ex.Message}");
            }
         }
      }
      
      Console.WriteLine("readyok");
      Console.Out.Flush();
   }
   
   /// <summary>
   ///    Shows help information.
   /// </summary>
   private void ShowHelp()
   {
      Console.WriteLine("Meridian Chess Engine - UCI Commands");
      Console.WriteLine("====================================");
      Console.WriteLine();
      Console.WriteLine("Standard UCI commands:");
      Console.WriteLine("  uci              - Start UCI protocol");
      Console.WriteLine("  isready          - Check if engine is ready");
      Console.WriteLine("  ucinewgame       - Start a new game");
      Console.WriteLine("  position         - Set position (startpos or fen)");
      Console.WriteLine("  go               - Start calculating");
      Console.WriteLine("  stop             - Stop calculating");
      Console.WriteLine("  setoption        - Set engine options");
      Console.WriteLine("  quit             - Exit the program");
      Console.WriteLine();
      Console.WriteLine("Additional commands:");
      Console.WriteLine("  d/display        - Display current position");
      Console.WriteLine("  help             - Show this help");
      Console.WriteLine();
      Console.WriteLine("Examples:");
      Console.WriteLine("  position startpos");
      Console.WriteLine("  position fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
      Console.WriteLine("  go depth 10");
      Console.WriteLine("  go movetime 5000");
      Console.WriteLine("  setoption name Hash value 256");
   }
   
   /// <summary>
   ///    Stops the current search and cleans up resources.
   /// </summary>
   private void StopSearchAndCleanup(bool suppressOutput)
   {
      // Set suppress flag before any other operations
      if (suppressOutput)
         suppressBestMove = true;
         
      // Stop the search engine immediately
      searchEngine.StopSearch();
      
      // Handle cancellation and cleanup
      lock (searchLock)
      {
         if (searchCts != null && !isDisposing)
         {
            try
            {
               // Cancel if not already cancelled
               if (!searchCts.Token.IsCancellationRequested)
               {
                  searchCts.Cancel();
               }
               
               // Wait for search task to complete if needed
               if (searchTask is { IsCompleted: false })
               {
                  // Use a shorter timeout for position changes
                  var timeout = suppressOutput ? 100 : 1000;
                  
                  try
                  {
                     if (!searchTask.Wait(timeout))
                     {
                        // Task didn't complete in time, but we need to move on
                        Console.Error.WriteLine("Warning: Search task did not complete in time");
                     }
                  }
                  catch (AggregateException ae)
                  {
                     // Log any non-cancellation exceptions
                     foreach (var ex in ae.InnerExceptions)
                     {
                        if (!(ex is OperationCanceledException))
                        {
                           Console.Error.WriteLine($"Search task error: {ex.Message}");
                        }
                     }
                  }
               }
            }
            catch (Exception ex)
            {
               Console.Error.WriteLine($"Error during search cleanup: {ex.Message}");
            }
            finally
            {
               // Clean up resources
               lock (disposeLock)
               {
                  if (!isDisposing)
                  {
                     isDisposing = true;
                     try
                     {
                        searchCts?.Dispose();
                     }
                     catch (Exception ex)
                     {
                        Console.Error.WriteLine($"Error disposing CancellationTokenSource: {ex.Message}");
                     }
                     finally
                     {
                        searchCts = null;
                        searchTask = null;
                        isDisposing = false;
                     }
                  }
               }
            }
         }
      }
   }
   
   /// <summary>
   ///    Starts a new search with the given parameters.
   /// </summary>
   private void StartNewSearch(Position position, int depth, int moveTime, bool infinite)
   {
      lock (searchLock)
      {
         // Reset the suppress flag
         suppressBestMove = false;
         
         // Create new cancellation token source
         searchCts = new CancellationTokenSource();
         var token = searchCts.Token;
         
         // Copy position to avoid modifications during search
         var searchPosition = position;
         
         // Start search task
         searchTask = Task.Run(() =>
         {
            try
            {
               // For infinite search, use a very long time and rely on stop command
               var searchTime = infinite ? int.MaxValue : moveTime;
               var bestMove = searchEngine.Search(searchPosition, depth, searchTime, token);
               var ponderMove = searchEngine.GetPonderMove();
               
               // Check if we should output the result
               // Always output bestmove unless explicitly suppressed
               if (!suppressBestMove)
               {
                  OutputBestMove(bestMove, ponderMove);
               }
            }
            catch (OperationCanceledException)
            {
               // Search was cancelled - check if we should output
               if (!suppressBestMove)
               {
                  var currentBestMove = searchEngine.GetBestMove();
                  var currentPonderMove = searchEngine.GetPonderMove();
                  OutputBestMove(currentBestMove, currentPonderMove);
               }
            }
            catch (Exception ex)
            {
               Console.Error.WriteLine($"Search error: {ex.Message}");
               Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
               
               // Try to output something even on error
               if (!suppressBestMove)
               {
                  Console.WriteLine("bestmove 0000");
                  Console.Out.Flush();
               }
            }
         }, token);
      }
   }
   
   /// <summary>
   ///    Outputs the best move to the GUI.
   /// </summary>
   private void OutputBestMove(Move move, Move ponderMove = default)
   {
      if (move != Move.Null)
      {
         if (!ponderMove.IsNull)
            Console.WriteLine($"bestmove {move.ToAlgebraic()} ponder {ponderMove.ToAlgebraic()}");
         else
            Console.WriteLine($"bestmove {move.ToAlgebraic()}");
      }
      else
         Console.WriteLine("bestmove 0000");
      Console.Out.Flush();
   }
}
