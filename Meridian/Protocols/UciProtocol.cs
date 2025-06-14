namespace Meridian.Protocols;

using System;
using System.Threading;
using System.Threading.Tasks;
using Core;

/// <summary>
/// Universal Chess Interface (UCI) protocol implementation
/// </summary>
public sealed class UciProtocol(Engine engine, Search search) : IProtocol, IDisposable
{
    private readonly UciLogger _logger = new();
    private bool _isRunning;
    private CancellationTokenSource? _searchCancellation;
    
    // UCI constants
    public const string EngineName = "Meridian";
    public const string EngineAuthor = "Meridian Team";
    public const string EngineVersion = "1.0";

    public string Name => "UCI";
    
    public bool IsRunning => _isRunning;
    
    public void Run(bool sendInitialUciResponse = false)
    {
        // Safety check
        if (engine == null || search == null)
            return;
            
        _isRunning = true;
        
        // If we've already consumed the "uci" command in Main, send the response
        if (sendInitialUciResponse)
        {
            _logger.LogInfo("Initial UCI response requested");
            HandleUci();
        }
        
        _logger.LogInfo("Starting UCI main loop");
        
        while (_isRunning)
        {
            try
            {
                var input = Console.ReadLine();
                _logger.LogInput(input);
                
                if (input == null) // EOF
                {
                    _logger.LogInfo("EOF received, exiting");
                    _isRunning = false;
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                    
                var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;
                    
                ProcessCommand(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in main loop: {ex.Message}");
                // Continue on any read errors
            }
        }
        
        _logger.LogInfo("UCI main loop ended");
    }
    
    public void Stop()
    {
        _isRunning = false;
        _searchCancellation?.Cancel();
    }
    
    public void Dispose() => _logger.Dispose();

    private void SendOutput(string output)
    {
        _logger.LogOutput(output);
        Console.WriteLine(output);
        Console.Out.Flush();
    }
    
    private void ProcessCommand(string[] tokens)
    {
        // UCI commands are case-sensitive, but some GUIs might send them in different cases
        // We'll handle both for compatibility
        string command = tokens[0];
        _logger.LogInfo($"Processing command: {command} (tokens: {string.Join(" ", tokens)})");
        
        switch (command)
        {
            case "uci":
            case "UCI":
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
        }
    }
    
    private void HandleUci()
    {
        SendOutput($"id name {EngineName} {EngineVersion}");
        SendOutput($"id author {EngineAuthor}");
        
        // Options
        SendOutput("option name Hash type spin default 128 min 1 max 2048");
        SendOutput("option name Threads type spin default 1 min 1 max 1");
        SendOutput("option name Ponder type check default false");
        
        SendOutput("uciok");
    }
    
    private void HandleIsReady()
    {
        SendOutput("readyok");
    }
    
    private void HandleNewGame()
    {
        _logger.LogInfo("New game command received");
        engine.NewGame();
        search.ClearTT();
        _logger.LogInfo("New game initialized");
    }
    
    private void HandlePosition(string[] tokens)
    {
        _logger.LogInfo($"HandlePosition called with {tokens.Length} tokens");
        
        if (tokens.Length < 2)
        {
            _logger.LogError("Position command too short");
            return;
        }
            
        int moveIndex = 2;
        
        if (tokens[1] == "startpos")
        {
            _logger.LogInfo("Setting startpos");
            engine.SetPosition("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }
        else if (tokens[1] == "fen" && tokens.Length >= 8)
        {
            // Reconstruct FEN string
            string fen = string.Join(" ", tokens[2], tokens[3], tokens[4], tokens[5], tokens[6], tokens[7]);
            _logger.LogInfo($"Setting FEN: {fen}");
            engine.SetPosition(fen);
            moveIndex = 8;
        }
        else
        {
            _logger.LogError($"Invalid position command: {string.Join(" ", tokens)}");
            return; // Invalid position command
        }
        
        // Apply moves if any
        if (moveIndex < tokens.Length && tokens[moveIndex] == "moves")
        {
            _logger.LogInfo($"Applying {tokens.Length - moveIndex - 1} moves");
            for (int i = moveIndex + 1; i < tokens.Length; i++)
            {
                _logger.LogInfo($"Parsing move: {tokens[i]}");
                if (!TryParseMove(tokens[i], out Move move))
                {
                    _logger.LogError($"Failed to parse move: {tokens[i]}");
                    break;
                }
                    
                _logger.LogInfo($"Making move: {move}");
                engine.MakeMove(move);
            }
        }
        
        _logger.LogInfo("Position command completed");
    }
    
    private void HandleGo(string[] tokens)
    {
        _logger.LogInfo($"HandleGo called with {tokens.Length} tokens: {string.Join(" ", tokens)}");
        
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
        _logger.LogInfo($"Calculated think time: {timeMs}ms (wtime={wtime}, btime={btime}, winc={winc}, binc={binc}, movetime={movetime}, infinite={infinite}, depth={depth})");
        
        // Start search in background
        var token = _searchCancellation.Token;
        _logger.LogInfo("Starting search task");
        Task.Run(() => DoSearch(depth, timeMs, token), token);
    }
    
    private void HandleStop()
    {
        _logger.LogInfo("Stop command received");
        _searchCancellation?.Cancel();
        search.Stop();
        _logger.LogInfo("Search stopped");
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
        _logger.LogSearch($"DoSearch started - depth={depth}, timeMs={timeMs}");
        
        try
        {
            var board = engine.GetBoard();
            _logger.LogSearch($"Got board state - SideToMove={board.SideToMove}");
            
            // Register cancellation with search
            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => {
                    _logger.LogSearch("Search cancellation requested");
                    search.Stop();
                });
            }
            
            _logger.LogSearch("Calling FindBestMove");
            var bestMove = search.FindBestMove(ref board, depth, timeMs);
            _logger.LogSearch($"FindBestMove returned: {bestMove} (Data={bestMove.Data})");
            
            // Always send bestmove if we have one, even if search was stopped
            if (bestMove.Data != 0)
            {
                SendOutput($"bestmove {bestMove}");
            }
            else
            {
                _logger.LogSearch($"No valid move found - moveData={bestMove.Data}");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogSearch("Search cancelled via OperationCanceledException");
            // Search was cancelled - normal operation
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in DoSearch: {ex}");
        }
        
        _logger.LogSearch("DoSearch completed");
    }
    
    private long CalculateThinkTime(long wtime, long btime, long winc, long binc, long movetime, bool infinite)
    {
        if (infinite)
            return long.MaxValue;
            
        if (movetime > 0)
            return movetime;
            
        // Simple time management
        var board = engine.GetBoard();
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
        var board = engine.GetBoard();
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
                
                // Check if our king is in check after the move (using original side to move)
                ulong king = board.SideToMove == Color.White ? testBoard.WhiteKing : testBoard.BlackKing;
                if (king != 0)
                {
                    Square kingSquare = (Square)Bitboard.BitScanForward(king);
                    Color enemyColor = board.SideToMove == Color.White ? Color.Black : Color.White;
                    if (!Attacks.IsSquareAttacked(ref testBoard, kingSquare, enemyColor))
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