#nullable enable

using Meridian.Core.Board;
using Meridian.Core.Search;
using Meridian.Core.MoveGeneration;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Meridian.Core.Protocol.UCI
{
    public sealed class UciEngine : IDisposable
    {
        private Meridian.Core.Search.ThreadPool _threadPool;
        private Position _position;
        private Thread? _searchThread;
        private int _hashSize = 128;
        private int _threadCount = 1;

        public UciEngine()
        {
            _position = Position.StartingPosition();
            _threadPool = new Meridian.Core.Search.ThreadPool(_threadCount, _hashSize);
        }

        public void Dispose()
        {
            _threadPool?.Dispose();
            _searchThread?.Join(100);
        }

        public void ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

#pragma warning disable CA1308 // UCI protocol requires lowercase commands
            switch (parts[0].ToLowerInvariant())
#pragma warning restore CA1308
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
                case "setoption":
                    HandleSetOption(parts);
                    break;
                case "debug":
                    HandleDebug();
                    break;
                default:
                    UciOutput.Error($"Unknown command: {parts[0]}");
                    break;
            }
        }

        private static void HandleUci()
        {
            UciOutput.WriteLine("id name Meridian");
            UciOutput.WriteLine("id author Meridian Team");
            UciOutput.WriteLine("");
            UciOutput.WriteLine("option name Hash type spin default 128 min 1 max 2048");
            UciOutput.WriteLine("option name Threads type spin default 1 min 1 max 128");
            UciOutput.WriteLine("uciok");
        }

        private static void HandleIsReady()
        {
            UciOutput.WriteLine("readyok");
        }

        private void HandleNewGame()
        {
            _position = Position.StartingPosition();
            _threadPool.ResizeTranspositionTable(_hashSize);
        }

        private void HandlePosition(string[] parts)
        {
            if (parts.Length < 2)
            {
                return;
            }

            if (parts[1] == "startpos")
            {
                _position = Position.StartingPosition();
            }
            else if (parts[1] == "fen")
            {
                if (parts.Length < 3)
                {
                    return;
                }

                var movesIndex = Array.IndexOf(parts, "moves");
                var fenEndIndex = movesIndex == -1 ? parts.Length : movesIndex;
                var fen = string.Join(" ", parts.Skip(2).Take(fenEndIndex - 2));
                var positionResult = Position.FromFen(fen);
                if (positionResult.IsFailure)
                {
                    return;
                }
                _position = positionResult.Value;
            }
            else
            {
                return;
            }

            var movesIdx = Array.IndexOf(parts, "moves");
            if (movesIdx >= 0 && movesIdx < parts.Length - 1)
            {
                for (var i = movesIdx + 1; i < parts.Length; i++)
                {
                    var moveStr = parts[i];
                    var move = ParseMove(moveStr, _position);
                    if (move == Move.None)
                    {
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
                try
                {
                    // Start parallel search on all threads
                    _threadPool.StartSearch(_position, limits);
                    
                    // Start reporting thread with event-based signaling
                    var isSearching = true;
                    var updateSignal = new ManualResetEventSlim(false);
                    var reportTimer = new System.Threading.Timer(_ => updateSignal.Set(), null, 500, 500);
                    
                    // Set up progress callback to signal updates
                    _threadPool.OnProgressUpdate = () => updateSignal.Set();
                    
                    var reportThread = new Thread(() =>
                    {
                        var lastDepth = 0;
                        var lastScore = int.MinValue;
                        var lastPv = string.Empty;
                        var lastNodes = 0L;
                        var lastReportTime = DateTime.UtcNow;
                        
                        while (isSearching)
                        {
                            // Wait for signal or timeout after 100ms
                            if (updateSignal.Wait(100))
                            {
                                updateSignal.Reset();
                            }
                            
                            if (!isSearching) break;
                            
                            var info = _threadPool.GetAggregatedInfo();
                            
                            // Copy PV without modifying the original
                            var pvMoves = new List<Move>();
                            var tempPv = new Queue<Move>(info.PrincipalVariation);
                            while (tempPv.Count > 0)
                            {
                                pvMoves.Add(tempPv.Dequeue());
                            }
                            var pvString = string.Join(" ", pvMoves.Select(m => m.ToUci()));
                            
                            // Report if something substantial changed
                            var now = DateTime.UtcNow;
                            var depthChanged = info.Depth > lastDepth;
                            var scoreChanged = info.Score != lastScore;
                            var pvChanged = pvString != lastPv;
                            var significantNodeIncrease = info.Nodes > lastNodes + 100000; // 100k nodes
                            var timeSinceLastReport = (now - lastReportTime).TotalMilliseconds;
                            
                            // Always report depth changes immediately, or periodic updates for same depth
                            var shouldReport = depthChanged || 
                                             (timeSinceLastReport >= 500 && (scoreChanged || pvChanged || significantNodeIncrease));
                            
                            if (shouldReport && info.Depth > 0)
                            {
                                // Build info string
                                var infoStr = $"info depth {info.Depth}";
                                
                                // Add selective depth
                                var selDepth = _threadPool.GetMaxSelectiveDepth();
                                infoStr += $" seldepth {selDepth}";
                                
                                // Add multipv (always 1 for now)
                                infoStr += " multipv 1";
                                
                                // Add score
                                if (Math.Abs(info.Score) >= SearchConstants.MateInMaxPly)
                                {
                                    var mateIn = (SearchConstants.MateScore - Math.Abs(info.Score) + 1) / 2;
                                    if (info.Score < 0) mateIn = -mateIn;
                                    infoStr += $" score mate {mateIn}";
                                }
                                else
                                {
                                    infoStr += $" score cp {info.Score}";
                                }
                                
                                // Calculate hashfull
                                var hashfull = _threadPool.GetHashfull();
                                
                                // Add nodes and performance info
                                infoStr += $" nodes {info.Nodes} nps {info.NodesPerSecond} hashfull {hashfull} tbhits 0 time {info.Time}";
                                
                                // Add principal variation
                                if (pvMoves.Count > 0)
                                {
                                    infoStr += $" pv {pvString}";
                                }
                                
                                UciOutput.WriteLine(infoStr);
                                
                                // Update tracking variables
                                lastDepth = info.Depth;
                                lastScore = info.Score;
                                lastPv = pvString;
                                lastNodes = info.Nodes;
                                lastReportTime = now;
                            }
                        }
                        
                        updateSignal.Dispose();
                    })
                    {
                        IsBackground = true
                    };
                    reportThread.Start();
                    
                    // Wait for search to complete
                    var bestMove = _threadPool.WaitForBestMove();
                    
                    // Stop reporting thread
                    isSearching = false;
                    reportTimer.Dispose();
                    updateSignal.Set(); // Wake up the thread
                    reportThread.Join(150);
                    
                    if (bestMove != Move.None)
                    {
                        UciOutput.WriteLine($"bestmove {bestMove.ToUci()}");
                    }
                    else
                    {
                        UciOutput.WriteLine("bestmove 0000");
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    UciOutput.Error($"ERROR in search thread: {ex.Message}");
                    UciOutput.Error($"Stack trace: {ex.StackTrace}");
                    UciOutput.WriteLine("bestmove 0000");
                }
            })
            {
                IsBackground = true
            };

            _searchThread.Start();
        }

        private void HandleStop()
        {
            _threadPool.StopAll();
            _searchThread?.Join(100);
        }

        private void HandleQuit()
        {
            _threadPool.StopAll();
            _searchThread?.Join(100);
            Environment.Exit(0);
        }

        private void HandleDebug()
        {
            Console.WriteLine("\n=== DEBUG INFO ===");
            Console.WriteLine($"Current position FEN: {_position.ToFen()}");
            Console.WriteLine($"Side to move: {_position.SideToMove}");
            Console.WriteLine($"Castling rights: {_position.CastlingRights}");
            Console.WriteLine($"En passant: {_position.EnPassantSquare}");
            Console.WriteLine($"Halfmove clock: {_position.HalfmoveClock}");
            Console.WriteLine($"Fullmove: {_position.FullmoveNumber}");
            Console.WriteLine("\nBoard representation:");
            for (int rank = 7; rank >= 0; rank--)
            {
                Console.Write($"{rank + 1} ");
                for (int file = 0; file < 8; file++)
                {
                    var square = (Square)(rank * 8 + file);
                    var piece = _position.GetPiece(square);
                    var pieceChar = GetPieceChar(piece);
                    Console.Write($"{pieceChar} ");
                }
                Console.WriteLine();
            }
            Console.WriteLine("  a b c d e f g h");
            Span<Move> moveBuffer = stackalloc Move[218];
            var moves = new MoveList(moveBuffer);
            var moveGen = new MoveGenerator();
            moveGen.GenerateMoves(_position, ref moves);
            Console.WriteLine($"\nLegal moves: {moves.Count}");
            var moveStrings = new List<string>();
            for (int i = 0; i < moves.Count; i++)
            {
                moveStrings.Add(moves[i].ToUci());
            }
            moveStrings.Sort();
            Console.WriteLine("Move list:");
            foreach (var move in moveStrings)
            {
                Console.Write($"{move} ");
            }
            Console.WriteLine();
            var eval = Evaluation.Evaluator.Evaluate(_position);
            Console.WriteLine($"\nStatic evaluation: {eval} cp");
            Console.WriteLine("=== END DEBUG ===\n");
        }

        private static char GetPieceChar(Piece piece) => piece switch
        {
            Piece.WhitePawn => 'P',
            Piece.WhiteKnight => 'N',
            Piece.WhiteBishop => 'B',
            Piece.WhiteRook => 'R',
            Piece.WhiteQueen => 'Q',
            Piece.WhiteKing => 'K',
            Piece.BlackPawn => 'p',
            Piece.BlackKnight => 'n',
            Piece.BlackBishop => 'b',
            Piece.BlackRook => 'r',
            Piece.BlackQueen => 'q',
            Piece.BlackKing => 'k',
            _ => '.'
        };

        private void HandleSetOption(string[] parts)
        {
            if (parts.Length < 5 || parts[1] != "name" || parts[3] != "value")
            {
                UciOutput.Error("Invalid setoption format");
                return;
            }

#pragma warning disable CA1308 // UCI protocol requires lowercase option names
            var optionName = parts[2].ToLowerInvariant();
#pragma warning restore CA1308
            var value = parts[4];

            switch (optionName)
            {
                case "hash":
                    if (int.TryParse(value, out var hashMb) && hashMb >= 1 && hashMb <= 2048)
                    {
                        _hashSize = hashMb;
                        // Recreate thread pool with new hash size
                        _threadPool?.Dispose();
                        _threadPool = new Meridian.Core.Search.ThreadPool(_threadCount, _hashSize);
                    }
                    break;
                case "threads":
                    if (int.TryParse(value, out var threads) && threads >= 1 && threads <= 128)
                    {
                        _threadCount = threads;
                        // Recreate thread pool with new thread count
                        _threadPool?.Dispose();
                        _threadPool = new Meridian.Core.Search.ThreadPool(_threadCount, _hashSize);
                    }
                    break;
                default:
                    UciOutput.Error($"Unknown option: {optionName}");
                    break;
            }
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
}