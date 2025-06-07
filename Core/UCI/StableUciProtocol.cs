namespace Meridian.Core.UCI;

using System.Text;
using MoveGeneration;
using Search;

/// <summary>
/// Stable UCI protocol implementation following Stockfish's proven pattern.
/// </summary>
public class StableUciProtocol
{
    public const string EngineName = "Meridian";
    public const string EngineAuthor = "Houijasu";
    
    private Position position = Position.StartingPosition();
    private readonly SearchEngine engine = new();
    private Thread? searchThread;
    private readonly object searchLock = new();
    
    /// <summary>
    /// Main UCI loop - processes commands synchronously.
    /// </summary>
    public void Run()
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            ProcessCommand(line.Trim());
            
            if (line == "quit")
                break;
        }
    }
    
    private void ProcessCommand(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return;
        
        var tokens = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return;
        
        // Process commands synchronously in main thread
        switch (tokens[0])
        {
            case "uci":
                SendId();
                break;
                
            case "isready":
                WaitForSearchFinished();
                Console.WriteLine("readyok");
                break;
                
            case "ucinewgame":
                WaitForSearchFinished();
                engine.ClearTT();
                engine.ClearMoveOrdering();
                break;
                
            case "position":
                WaitForSearchFinished();
                SetPosition(tokens);
                break;
                
            case "go":
                Go(tokens);
                break;
                
            case "stop":
                StopThinking();
                break;
                
            case "quit":
                StopThinking();
                break;
                
            // Debug commands
            case "d":
                Console.WriteLine(position.ToString());
                break;
        }
    }
    
    private void SendId()
    {
        Console.WriteLine($"id name {EngineName}");
        Console.WriteLine($"id author {EngineAuthor}");
        Console.WriteLine("option name Hash type spin default 128 min 1 max 16384");
        Console.WriteLine("uciok");
    }
    
    private void SetPosition(string[] tokens)
    {
        var idx = 1;
        
        if (idx < tokens.Length && tokens[idx] == "startpos")
        {
            position = Position.StartingPosition();
            idx++;
        }
        else if (idx < tokens.Length && tokens[idx] == "fen")
        {
            idx++;
            var fen = new StringBuilder();
            
            while (idx < tokens.Length && tokens[idx] != "moves")
            {
                if (fen.Length > 0) fen.Append(' ');
                fen.Append(tokens[idx]);
                idx++;
            }
            
            try
            {
                position = Fen.Parse(fen.ToString());
            }
            catch
            {
                // Keep current position on error
                return;
            }
        }
        
        // Apply moves
        if (idx < tokens.Length && tokens[idx] == "moves")
        {
            idx++;
            while (idx < tokens.Length)
            {
                var move = ParseMove(tokens[idx]);
                if (move != Move.Null)
                {
                    position.MakeMove(move);
                }
                idx++;
            }
        }
    }
    
    private void Go(string[] tokens)
    {
        // Wait for any previous search to finish
        WaitForSearchFinished();
        
        // Parse limits
        var limits = ParseGoCommand(tokens);
        
        // Start search in background thread
        lock (searchLock)
        {
            var searchPos = position; // Copy current position
            
            searchThread = new Thread(() =>
            {
                try
                {
                    var bestMove = engine.Search(searchPos, limits.Depth, limits.Time);
                    
                    // Always output bestmove when search completes
                    if (bestMove != Move.Null)
                        Console.WriteLine($"bestmove {bestMove.ToAlgebraic()}");
                    else
                        Console.WriteLine("bestmove 0000");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Search error: {ex.Message}");
                    Console.WriteLine("bestmove 0000");
                }
            })
            {
                Name = "Search",
                IsBackground = false // Important: not a background thread
            };
            
            searchThread.Start();
        }
    }
    
    private void StopThinking()
    {
        engine.StopSearch();
        WaitForSearchFinished();
    }
    
    private void WaitForSearchFinished()
    {
        lock (searchLock)
        {
            if (searchThread is { IsAlive: true })
            {
                engine.StopSearch();
                
                // Wait for thread to finish (with timeout)
                if (!searchThread.Join(5000))
                {
                    // Last resort - should rarely happen
                    Console.Error.WriteLine("Warning: Search thread did not finish in time");
                    
                    // Output something to unblock GUI
                    var move = engine.GetBestMove();
                    if (move != Move.Null)
                        Console.WriteLine($"bestmove {move.ToAlgebraic()}");
                    else
                        Console.WriteLine("bestmove 0000");
                }
                
                searchThread = null;
            }
        }
    }
    
    private SearchLimits ParseGoCommand(string[] tokens)
    {
        var limits = new SearchLimits
        {
            Depth = SearchConstants.MaxDepth,
            Time = int.MaxValue
        };
        
        for (int i = 1; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "depth":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var d))
                    {
                        limits.Depth = Math.Min(d, SearchConstants.MaxDepth);
                        i++;
                    }
                    break;
                    
                case "movetime":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var mt))
                    {
                        limits.Time = mt;
                        i++;
                    }
                    break;
                    
                case "infinite":
                    limits.Time = int.MaxValue;
                    break;
                    
                case "wtime":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var wt))
                    {
                        if (position.SideToMove == Color.White)
                        {
                            // Simple time management
                            limits.Time = Math.Min(limits.Time, wt / 20 + 50);
                        }
                        i++;
                    }
                    break;
                    
                case "btime":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var bt))
                    {
                        if (position.SideToMove == Color.Black)
                        {
                            limits.Time = Math.Min(limits.Time, bt / 20 + 50);
                        }
                        i++;
                    }
                    break;
            }
        }
        
        return limits;
    }
    
    private Move ParseMove(string moveStr)
    {
        if (moveStr.Length < 4) return Move.Null;
        
        var from = ParseSquare(moveStr[..2]);
        var to = ParseSquare(moveStr[2..4]);
        
        if (from == Square.None || to == Square.None) return Move.Null;
        
        // Generate legal moves to validate
        Span<Move> moveBuffer = stackalloc Move[256];
        var moveList = new MoveList(moveBuffer);
        MoveGenerator.GenerateMoves(in position, ref moveList);
        
        for (int i = 0; i < moveList.Count; i++)
        {
            var move = moveList[i];
            if (move.From == from && move.To == to)
            {
                // Handle promotions
                if (moveStr.Length > 4 && move.IsPromotion)
                {
                    var promo = char.ToLower(moveStr[4]);
                    var type = move.GetPromotionType();
                    
                    var match = promo switch
                    {
                        'q' => type == PieceType.Queen,
                        'r' => type == PieceType.Rook,
                        'b' => type == PieceType.Bishop,
                        'n' => type == PieceType.Knight,
                        _ => false
                    };
                    
                    if (match) return move;
                }
                else if (moveStr.Length == 4 && !move.IsPromotion)
                {
                    return move;
                }
            }
        }
        
        return Move.Null;
    }
    
    private static Square ParseSquare(string str)
    {
        if (str.Length != 2) return Square.None;
        
        var file = str[0] - 'a';
        var rank = str[1] - '1';
        
        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            return Square.None;
            
        return (Square)(rank * 8 + file);
    }
    
    private struct SearchLimits
    {
        public int Depth;
        public int Time;
    }
}