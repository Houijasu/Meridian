namespace Meridian.Protocols;

using System;
using System.Threading;
using System.Threading.Tasks;
using Meridian.Core;

/// <summary>
/// Universal Chess Interface (UCI) protocol implementation
/// </summary>
public sealed class UciProtocol : IProtocol
{
    private readonly Engine _engine;
    private readonly Search _search;
    private bool _isRunning;
    private CancellationTokenSource? _searchCancellation;
    
    // UCI constants
    private const string EngineName = "Meridian";
    private const string EngineAuthor = "Meridian Team";
    private const string EngineVersion = "1.0";
    
    public UciProtocol(Engine engine, Search search)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _search = search ?? throw new ArgumentNullException(nameof(search));
    }
    
    public string Name => "UCI";
    
    public bool IsRunning => _isRunning;
    
    public void Run()
    {
        _isRunning = true;
        
        while (_isRunning)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;
                
            var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;
                
            ProcessCommand(tokens);
        }
    }
    
    public void Stop()
    {
        _isRunning = false;
        _searchCancellation?.Cancel();
    }
    
    private void ProcessCommand(string[] tokens)
    {
        switch (tokens[0].ToLowerInvariant())
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
                
            case "quit":
                HandleQuit();
                break;
                
            case "setoption":
                HandleSetOption(tokens);
                break;
                
            default:
                // Unknown command - UCI spec says to ignore
                break;
        }
    }
    
    private void HandleUci()
    {
        Console.WriteLine($"id name {EngineName} {EngineVersion}");
        Console.WriteLine($"id author {EngineAuthor}");
        
        // Options
        Console.WriteLine("option name Hash type spin default 128 min 1 max 2048");
        Console.WriteLine("option name Threads type spin default 1 min 1 max 1");
        Console.WriteLine("option name Ponder type check default false");
        
        Console.WriteLine("uciok");
    }
    
    private void HandleIsReady()
    {
        Console.WriteLine("readyok");
    }
    
    private void HandleNewGame()
    {
        _engine.NewGame();
        _search.ClearTT();
    }
    
    private void HandlePosition(string[] tokens)
    {
        if (tokens.Length < 2)
            return;
            
        int moveIndex = 2;
        
        if (tokens[1] == "startpos")
        {
            _engine.SetPosition("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }
        else if (tokens[1] == "fen" && tokens.Length >= 8)
        {
            // Reconstruct FEN string
            string fen = string.Join(" ", tokens[2], tokens[3], tokens[4], tokens[5], tokens[6], tokens[7]);
            _engine.SetPosition(fen);
            moveIndex = 8;
        }
        else
        {
            return; // Invalid position command
        }
        
        // Apply moves if any
        if (moveIndex < tokens.Length && tokens[moveIndex] == "moves")
        {
            for (int i = moveIndex + 1; i < tokens.Length; i++)
            {
                if (!TryParseMove(tokens[i], out Move move))
                    break;
                    
                _engine.MakeMove(move);
            }
        }
    }
    
    private void HandleGo(string[] tokens)
    {
        // Cancel any ongoing search
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        
        // Parse time controls
        long wtime = 0, btime = 0, winc = 0, binc = 0;
        int depth = 100;
        long movetime = 0;
        bool infinite = false;
        
        for (int i = 1; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "infinite":
                    infinite = true;
                    break;
                case "depth":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int d))
                        depth = d;
                    i++;
                    break;
                case "movetime":
                    if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long mt))
                        movetime = mt;
                    i++;
                    break;
                case "wtime":
                    if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long wt))
                        wtime = wt;
                    i++;
                    break;
                case "btime":
                    if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long bt))
                        btime = bt;
                    i++;
                    break;
                case "winc":
                    if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long wi))
                        winc = wi;
                    i++;
                    break;
                case "binc":
                    if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long bi))
                        binc = bi;
                    i++;
                    break;
            }
        }
        
        // Calculate time for this move
        long timeMs = CalculateThinkTime(wtime, btime, winc, binc, movetime, infinite);
        
        // Start search in background
        var token = _searchCancellation.Token;
        Task.Run(() => DoSearch(depth, timeMs, token), token);
    }
    
    private void HandleStop()
    {
        _searchCancellation?.Cancel();
        _search.Stop();
    }
    
    private void HandleQuit()
    {
        Stop();
    }
    
    private void HandleSetOption(string[] tokens)
    {
        // Parse "setoption name <name> value <value>"
        if (tokens.Length < 5)
            return;
            
        if (tokens[1] != "name" || tokens[3] != "value")
            return;
            
        string name = tokens[2];
        string value = string.Join(" ", tokens.Skip(4));
        
        switch (name.ToLowerInvariant())
        {
            case "hash":
                if (int.TryParse(value, out int hashMb))
                {
                    // TODO: Resize transposition table
                }
                break;
                
            // Add more options as needed
        }
    }
    
    private void DoSearch(int depth, long timeMs, CancellationToken cancellation)
    {
        try
        {
            var board = _engine.GetBoard();
            
            // Register cancellation with search
            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => _search.Stop());
            }
            
            var bestMove = _search.FindBestMove(ref board, depth, timeMs);
            
            if (!cancellation.IsCancellationRequested && bestMove.Data != 0)
            {
                Console.WriteLine($"bestmove {bestMove}");
            }
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled - normal operation
        }
    }
    
    private long CalculateThinkTime(long wtime, long btime, long winc, long binc, long movetime, bool infinite)
    {
        if (infinite)
            return long.MaxValue;
            
        if (movetime > 0)
            return movetime;
            
        // Simple time management
        var board = _engine.GetBoard();
        long myTime = board.SideToMove == Color.White ? wtime : btime;
        long myInc = board.SideToMove == Color.White ? winc : binc;
        
        if (myTime == 0)
            return 5000; // Default 5 seconds
            
        // Use 2.5% of remaining time plus 80% of increment
        long thinkTime = myTime / 40 + (myInc * 4) / 5;
        
        // Ensure we don't use all our time
        return Math.Min(thinkTime, myTime - 50);
    }
    
    private bool TryParseMove(string moveStr, out Move move)
    {
        move = default;
        
        if (moveStr.Length < 4 || moveStr.Length > 5)
            return false;
            
        // Parse squares
        int fromFile = moveStr[0] - 'a';
        int fromRank = moveStr[1] - '1';
        int toFile = moveStr[2] - 'a';
        int toRank = moveStr[3] - '1';
        
        if (fromFile < 0 || fromFile > 7 || fromRank < 0 || fromRank > 7 ||
            toFile < 0 || toFile > 7 || toRank < 0 || toRank > 7)
            return false;
            
        Square from = (Square)(fromRank * 8 + fromFile);
        Square to = (Square)(toRank * 8 + toFile);
        
        // Parse promotion if present
        Piece promotion = Piece.None;
        if (moveStr.Length == 5)
        {
            promotion = moveStr[4] switch
            {
                'q' => Piece.Queen,
                'r' => Piece.Rook,
                'b' => Piece.Bishop,
                'n' => Piece.Knight,
                _ => Piece.None
            };
            
            if (promotion == Piece.None)
                return false;
        }
        
        // Find the matching legal move
        var board = _engine.GetBoard();
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        for (int i = 0; i < moves.Count; i++)
        {
            var candidate = moves[i];
            if (candidate.From == from && candidate.To == to)
            {
                // Check promotion matches
                if (promotion != Piece.None && candidate.PromotionPiece != promotion)
                    continue;
                    
                // Verify move is legal
                BoardState testBoard = board;
                testBoard.MakeMove(candidate);
                
                ulong king = testBoard.SideToMove == Color.White ? board.WhiteKing : board.BlackKing;
                if (king != 0)
                {
                    Square kingSquare = (Square)Bitboard.BitScanForward(king);
                    if (!Attacks.IsSquareAttacked(ref testBoard, kingSquare, testBoard.SideToMove))
                    {
                        move = candidate;
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
}