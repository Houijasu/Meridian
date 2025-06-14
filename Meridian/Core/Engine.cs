namespace Meridian.Core;

public sealed class Engine
{
    private string _currentFen;
    private readonly Search _search;
    
    public Engine()
    {
        _currentFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        _search = new Search();
    }
    
    public void NewGame()
    {
        _currentFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    }
    
    public void SetPosition(string fen)
    {
        _currentFen = fen;
    }
    
    public Move Think(int depthLimit = 6, long timeLimitMs = 5000)
    {
        Console.WriteLine($"Thinking (depth={depthLimit}, time={timeLimitMs}ms)...");
        var board = FenParser.Parse(_currentFen);
        return _search.FindBestMove(ref board, depthLimit, timeLimitMs);
    }
    
    public bool MakeMove(Move move)
    {
        var board = FenParser.Parse(_currentFen);
        
        // Validate move is legal
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        bool found = false;
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == move.From && moves[i].To == move.To &&
                moves[i].PromotionPiece == move.PromotionPiece)
            {
                found = true;
                break;
            }
        }
        
        if (!found)
            return false;
        
        // Check if move is legal (doesn't leave king in check)
        BoardState copy = board;
        board.MakeMove(move);
        
        bool isLegal = !IsKingInCheck(ref board, board.SideToMove.Opposite());
        
        if (!isLegal)
        {
            return false;
        }
        
        // Update the FEN
        _currentFen = FenParser.ToFen(ref board);
        return true;
    }
    
    public bool MakeMove(string moveStr)
    {
        // Parse algebraic notation (e.g., "e2e4")
        if (moveStr.Length < 4)
            return false;
        
        Square from = ParseSquare(moveStr.Substring(0, 2));
        Square to = ParseSquare(moveStr.Substring(2, 2));
        
        if (from == Square.None || to == Square.None)
            return false;
        
        // Check for promotion
        Piece promotionPiece = Piece.None;
        if (moveStr.Length == 5)
        {
            promotionPiece = moveStr[4] switch
            {
                'q' => Piece.Queen,
                'r' => Piece.Rook,
                'b' => Piece.Bishop,
                'n' => Piece.Knight,
                _ => Piece.None
            };
        }
        
        var board = FenParser.Parse(_currentFen);
        
        // Find the matching move
        MoveList moves = new();
        MoveGenerator.GenerateAllMoves(ref board, ref moves);
        
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].From == from && moves[i].To == to)
            {
                // For promotions, check the piece matches
                if (moves[i].IsPromotion() && moves[i].PromotionPiece != promotionPiece)
                    continue;
                
                return MakeMove(moves[i]);
            }
        }
        
        return false;
    }
    
    public string GetPosition()
    {
        return _currentFen;
    }
    
    public BoardState GetBoard()
    {
        return FenParser.Parse(_currentFen);
    }
    
    public void PrintBoard()
    {
        var board = FenParser.Parse(_currentFen);
        
        Console.WriteLine("\n  a b c d e f g h");
        Console.WriteLine("  ---------------");
        
        for (int rank = 7; rank >= 0; rank--)
        {
            Console.Write($"{rank + 1}|");
            
            for (int file = 0; file < 8; file++)
            {
                Square sq = (Square)(rank * 8 + file);
                var (piece, color) = board.GetPieceAt(sq);
                
                char pieceChar = GetPieceChar(piece, color);
                Console.Write($"{pieceChar} ");
            }
            
            Console.WriteLine($"|{rank + 1}");
        }
        
        Console.WriteLine("  ---------------");
        Console.WriteLine("  a b c d e f g h\n");
        Console.WriteLine($"FEN: {GetPosition()}");
        Console.WriteLine($"Side to move: {board.SideToMove}");
    }
    
    private static char GetPieceChar(Piece piece, Color color)
    {
        if (piece == Piece.None)
            return '.';
        
        char pieceChar = piece switch
        {
            Piece.Pawn => 'p',
            Piece.Knight => 'n',
            Piece.Bishop => 'b',
            Piece.Rook => 'r',
            Piece.Queen => 'q',
            Piece.King => 'k',
            _ => '?'
        };
        
        return color == Color.White ? char.ToUpper(pieceChar) : pieceChar;
    }
    
    private static Square ParseSquare(string sq)
    {
        if (sq.Length != 2)
            return Square.None;
        
        int file = sq[0] - 'a';
        int rank = sq[1] - '1';
        
        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            return Square.None;
        
        return (Square)(rank * 8 + file);
    }
    
    private static bool IsKingInCheck(ref BoardState board, Color color)
    {
        ulong king = color == Color.White ? board.WhiteKing : board.BlackKing;
        if (king == 0) return false;
        Square kingSquare = (Square)Bitboard.BitScanForward(king);
        return Attacks.IsSquareAttacked(ref board, kingSquare, color.Opposite());
    }
}