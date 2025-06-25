#nullable enable

using Meridian.Core.Board;
using Meridian.Core.Search;

namespace Meridian.Core.Protocol.UCI;

public sealed class UciEngine
{
    private readonly SearchEngine _searchEngine = new();
    private Position _position = new();
    private Thread? _searchThread;

    public void ProcessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var cmd = parts[0].ToLowerInvariant();

        switch (cmd)
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
                HandlePosition(parts);
                break;
            case "go":
                HandleGo(parts);
                break;
            case "stop":
                HandleStop();
                break;
            case "quit":
                HandleQuit();
                break;
            default:
                UciOutput.Error($"Unknown command: {cmd}");
                break;
        }
    }

    private void HandleUci()
    {
        Console.WriteLine("id name Meridian");
        Console.WriteLine("id author Meridian Team");
        Console.WriteLine();
        Console.WriteLine("option name Hash type spin default 128 min 1 max 2048");
        Console.WriteLine("option name Threads type spin default 1 min 1 max 128");
        Console.WriteLine("uciok");
    }

    private void HandleIsReady()
    {
        Console.WriteLine("readyok");
    }

    private void HandleNewGame()
    {
        _position = new Position();
    }

    private void HandlePosition(string[] parts)
    {
        if (parts.Length < 2)
        {
            UciOutput.Error("Invalid position command");
            return;
        }

        if (parts[1] == "startpos")
        {
            try
            {
                _position = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
            }
            catch (ArgumentException ex)
            {
                UciOutput.Error($"Failed to set start position: {ex.Message}");
                return;
            }
        }
        else if (parts[1] == "fen")
        {
            if (parts.Length < 8)
            {
                UciOutput.Error("Invalid FEN in position command");
                return;
            }

            var fen = string.Join(" ", parts.Skip(2).Take(6));
            try
            {
                _position = Position.FromFen(fen);
            }
            catch (ArgumentException ex)
            {
                UciOutput.Error($"Invalid FEN: {ex.Message}");
                return;
            }
        }

        var movesIndex = Array.IndexOf(parts, "moves");
        if (movesIndex >= 0 && movesIndex < parts.Length - 1)
        {
            for (var i = movesIndex + 1; i < parts.Length; i++)
            {
                var moveStr = parts[i];
                var move = ParseMove(moveStr, _position);
                if (move == Move.None)
                {
                    UciOutput.Error($"Invalid move: {moveStr}");
                    return;
                }
                _position.MakeMove(move);
            }
        }
    }

    private void HandleGo(string[] parts)
    {
        if (_searchThread?.IsAlive == true)
        {
            UciOutput.Error("Search already in progress");
            return;
        }

        var limits = ParseSearchLimits(parts);
        
        _searchThread = new Thread(() =>
        {
            var bestMove = _searchEngine.StartSearch(_position, limits);
            
            if (bestMove != Move.None)
            {
                var info = _searchEngine.SearchInfo;
                Console.WriteLine($"info depth {info.Depth} score cp {info.Score} nodes {info.Nodes} nps {info.NodesPerSecond} time {info.Time}");
                Console.WriteLine($"bestmove {bestMove.ToUci()}");
            }
            else
            {
                Console.WriteLine("bestmove 0000");
            }
        })
        {
            IsBackground = true
        };

        _searchThread.Start();
    }

    private void HandleStop()
    {
        _searchEngine.Stop();
        _searchThread?.Join(100);
    }

    private void HandleQuit()
    {
        _searchEngine.Stop();
        _searchThread?.Join(100);
        Environment.Exit(0);
    }

    private static SearchLimits ParseSearchLimits(string[] parts)
    {
        var limits = new SearchLimits();

        for (var i = 1; i < parts.Length; i++)
        {
            switch (parts[i])
            {
                case "wtime" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var wtime))
                        limits.WhiteTime = wtime;
                    i++;
                    break;
                case "btime" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var btime))
                        limits.BlackTime = btime;
                    i++;
                    break;
                case "winc" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var winc))
                        limits.WhiteIncrement = winc;
                    i++;
                    break;
                case "binc" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var binc))
                        limits.BlackIncrement = binc;
                    i++;
                    break;
                case "movestogo" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var mtg))
                        limits.MovesToGo = mtg;
                    i++;
                    break;
                case "depth" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var depth))
                        limits.Depth = depth;
                    i++;
                    break;
                case "movetime" when i + 1 < parts.Length:
                    if (int.TryParse(parts[i + 1], out var movetime))
                        limits.MoveTime = movetime;
                    i++;
                    break;
                case "infinite":
                    limits.Infinite = true;
                    break;
            }
        }

        return limits;
    }

    private Move ParseMove(string moveStr, Position position)
    {
        if (moveStr.Length < 4)
            return Move.None;

        var from = SquareExtensions.ParseSquare(moveStr[..2]);
        var to = SquareExtensions.ParseSquare(moveStr[2..4]);

        if (from == Square.None || to == Square.None)
            return Move.None;

        Span<Move> moveBuffer = stackalloc Move[218];
        var moves = new MoveList(moveBuffer);
        var moveGen = new MoveGeneration.MoveGenerator();
        moveGen.GenerateMoves(position, ref moves);
        
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (move.From == from && move.To == to)
            {
                if (moveStr.Length == 5 && move.IsPromotion)
                {
                    var promChar = moveStr[4];
                    var expectedPromo = promChar switch
                    {
                        'q' => PieceType.Queen,
                        'r' => PieceType.Rook,
                        'b' => PieceType.Bishop,
                        'n' => PieceType.Knight,
                        _ => PieceType.None
                    };
                    
                    if (move.PromotionType == expectedPromo)
                        return move;
                }
                else if (moveStr.Length == 4 && !move.IsPromotion)
                {
                    return move;
                }
            }
        }

        return Move.None;
    }
}