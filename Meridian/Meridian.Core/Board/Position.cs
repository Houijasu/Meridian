#nullable enable

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CSharpFunctionalExtensions;

namespace Meridian.Core.Board;

public sealed class Position
{
    public const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly Bitboard[] _pieceBitboards;
    private readonly Bitboard[] _colorBitboards;
    private readonly Piece[] _board;
    
    public Color SideToMove { get; private set; }
    public CastlingRights CastlingRights { get; private set; }
    public Square EnPassantSquare { get; private set; }
    public int HalfmoveClock { get; private set; }
    public int FullmoveNumber { get; private set; }
    public ulong ZobristKey { get; private set; }

    public Position()
    {
        _pieceBitboards = new Bitboard[7];
        _colorBitboards = new Bitboard[2];
        _board = new Piece[64];
        EnPassantSquare = Square.None;
    }

    public Position(Position other)
    {
        if (other == null)
        {
            _pieceBitboards = new Bitboard[7];
            _colorBitboards = new Bitboard[2];
            _board = new Piece[64];
            EnPassantSquare = Square.None;
            return;
        }
        
        _pieceBitboards = new Bitboard[7];
        _colorBitboards = new Bitboard[2];
        _board = new Piece[64];
        
        Array.Copy(other._pieceBitboards, _pieceBitboards, 7);
        Array.Copy(other._colorBitboards, _colorBitboards, 2);
        Array.Copy(other._board, _board, 64);
        
        SideToMove = other.SideToMove;
        CastlingRights = other.CastlingRights;
        EnPassantSquare = other.EnPassantSquare;
        HalfmoveClock = other.HalfmoveClock;
        FullmoveNumber = other.FullmoveNumber;
        ZobristKey = other.ZobristKey;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard GetBitboard(PieceType pieceType) => _pieceBitboards[(int)pieceType];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard GetBitboard(Color color) => _colorBitboards[(int)color];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard GetBitboard(Color color, PieceType pieceType) => 
        _pieceBitboards[(int)pieceType] & _colorBitboards[(int)color];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard OccupiedSquares() => _colorBitboards[0] | _colorBitboards[1];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bitboard EmptySquares() => ~OccupiedSquares();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Piece GetPiece(Square square) => _board[(int)square];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty(Square square) => _board[(int)square] == Piece.None;

    public void SetPiece(Square square, Piece piece)
    {
        RemovePiece(square);
        
        if (piece != Piece.None)
        {
            _board[(int)square] = piece;
            var color = piece.GetColor();
            var type = piece.Type();
            
            _pieceBitboards[(int)type] |= square.ToBitboard();
            _colorBitboards[(int)color] |= square.ToBitboard();
            
            ZobristKey ^= Zobrist.PieceKey(square, piece);
        }
    }

    public void RemovePiece(Square square)
    {
        var piece = _board[(int)square];
        if (piece != Piece.None)
        {
            _board[(int)square] = Piece.None;
            var color = piece.GetColor();
            var type = piece.Type();
            
            _pieceBitboards[(int)type] &= ~square.ToBitboard();
            _colorBitboards[(int)color] &= ~square.ToBitboard();
            
            ZobristKey ^= Zobrist.PieceKey(square, piece);
        }
    }

    public void MakeMove(Move move)
    {
        var from = move.From;
        var to = move.To;
        var piece = GetPiece(from);
        var pieceType = piece.Type();
        
        if (move.IsEnPassant)
        {
            var captureSquare = SideToMove == Color.White ? to - 8 : to + 8;
            RemovePiece((Square)captureSquare);
        }
        else if (move.IsCapture)
        {
            RemovePiece(to);
        }
        
        RemovePiece(from);
        
        if (move.IsPromotion)
        {
            SetPiece(to, PieceExtensions.MakePiece(SideToMove, move.PromotionType));
        }
        else
        {
            SetPiece(to, piece);
        }
        
        if (move.IsCastling)
        {
            Square rookFrom, rookTo;
            if (to == Square.G1)
            {
                rookFrom = Square.H1;
                rookTo = Square.F1;
            }
            else if (to == Square.C1)
            {
                rookFrom = Square.A1;
                rookTo = Square.D1;
            }
            else if (to == Square.G8)
            {
                rookFrom = Square.H8;
                rookTo = Square.F8;
            }
            else
            {
                rookFrom = Square.A8;
                rookTo = Square.D8;
            }
            
            var rook = GetPiece(rookFrom);
            RemovePiece(rookFrom);
            SetPiece(rookTo, rook);
        }
        
        var oldCastlingRights = CastlingRights;
        
        if (pieceType == PieceType.King)
        {
            CastlingRights &= SideToMove == Color.White ? ~CastlingRights.White : ~CastlingRights.Black;
        }
        else if (pieceType == PieceType.Rook)
        {
            if (from == Square.A1) CastlingRights &= ~CastlingRights.WhiteQueenside;
            else if (from == Square.H1) CastlingRights &= ~CastlingRights.WhiteKingside;
            else if (from == Square.A8) CastlingRights &= ~CastlingRights.BlackQueenside;
            else if (from == Square.H8) CastlingRights &= ~CastlingRights.BlackKingside;
        }
        
        if (to == Square.A1) CastlingRights &= ~CastlingRights.WhiteQueenside;
        else if (to == Square.H1) CastlingRights &= ~CastlingRights.WhiteKingside;
        else if (to == Square.A8) CastlingRights &= ~CastlingRights.BlackQueenside;
        else if (to == Square.H8) CastlingRights &= ~CastlingRights.BlackKingside;
        
        if (oldCastlingRights != CastlingRights)
        {
            ZobristKey ^= Zobrist.CastlingKey(oldCastlingRights);
            ZobristKey ^= Zobrist.CastlingKey(CastlingRights);
        }
        
        if (EnPassantSquare != Square.None)
        {
            ZobristKey ^= Zobrist.EnPassantKey(EnPassantSquare);
        }
        
        EnPassantSquare = move.IsDoublePush ? 
            (Square)((int)from + (SideToMove == Color.White ? 8 : -8)) : 
            Square.None;
            
        if (EnPassantSquare != Square.None)
        {
            ZobristKey ^= Zobrist.EnPassantKey(EnPassantSquare);
        }
        
        if (move.IsCapture || pieceType == PieceType.Pawn)
            HalfmoveClock = 0;
        else
            HalfmoveClock++;
        
        if (SideToMove == Color.Black)
            FullmoveNumber++;
        
        ZobristKey ^= Zobrist.SideKey();
        SideToMove = SideToMove == Color.White ? Color.Black : Color.White;
    }

    public static Result<Position> FromFen(string? fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            return Result.Failure<Position>("FEN string cannot be null or empty");
        
        var position = new Position();
        var parts = fen.Split(' ');
        
        if (parts.Length != 6)
            return Result.Failure<Position>("Invalid FEN string: expected 6 parts");
        
        var ranks = parts[0].Split('/');
        if (ranks.Length != 8)
            return Result.Failure<Position>("Invalid FEN board representation: expected 8 ranks");
        
        for (var rank = 7; rank >= 0; rank--)
        {
            var file = 0;
            foreach (var c in ranks[7 - rank])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                }
                else
                {
                    var piece = CharToPiece(c);
                    if (piece != Piece.None)
                    {
                        position.SetPiece(SquareExtensions.FromFileRank(file, rank), piece);
                    }
                    file++;
                }
            }
        }
        
        position.SideToMove = parts[1] == "w" ? Color.White : Color.Black;
        
        position.CastlingRights = CastlingRights.None;
        foreach (var c in parts[2])
        {
            position.CastlingRights |= c switch
            {
                'K' => CastlingRights.WhiteKingside,
                'Q' => CastlingRights.WhiteQueenside,
                'k' => CastlingRights.BlackKingside,
                'q' => CastlingRights.BlackQueenside,
                _ => CastlingRights.None
            };
        }
        
        position.EnPassantSquare = parts[3] == "-" ? Square.None : SquareExtensions.ParseSquare(parts[3]);
        
        if (!int.TryParse(parts[4], CultureInfo.InvariantCulture, out var halfmoveClock))
            return Result.Failure<Position>($"Invalid halfmove clock: {parts[4]}");
        position.HalfmoveClock = halfmoveClock;
        
        if (!int.TryParse(parts[5], CultureInfo.InvariantCulture, out var fullmoveNumber))
            return Result.Failure<Position>($"Invalid fullmove number: {parts[5]}");
        position.FullmoveNumber = fullmoveNumber;
        
        position.ZobristKey = Zobrist.ComputeKey(position);
        
        return Result.Success(position);
    }

    public string ToFen()
    {
        var sb = new StringBuilder();
        
        for (var rank = 7; rank >= 0; rank--)
        {
            var emptyCount = 0;
            for (var file = 0; file < 8; file++)
            {
                var piece = GetPiece(SquareExtensions.FromFileRank(file, rank));
                if (piece == Piece.None)
                {
                    emptyCount++;
                }
                else
                {
                    if (emptyCount > 0)
                    {
                        sb.Append(emptyCount);
                        emptyCount = 0;
                    }
                    sb.Append(PieceToChar(piece));
                }
            }
            if (emptyCount > 0)
            {
                sb.Append(emptyCount);
            }
            if (rank > 0)
            {
                sb.Append('/');
            }
        }
        
        sb.Append(' ');
        sb.Append(SideToMove == Color.White ? 'w' : 'b');
        sb.Append(' ');
        
        if (CastlingRights == CastlingRights.None)
        {
            sb.Append('-');
        }
        else
        {
            if ((CastlingRights & CastlingRights.WhiteKingside) != 0) sb.Append('K');
            if ((CastlingRights & CastlingRights.WhiteQueenside) != 0) sb.Append('Q');
            if ((CastlingRights & CastlingRights.BlackKingside) != 0) sb.Append('k');
            if ((CastlingRights & CastlingRights.BlackQueenside) != 0) sb.Append('q');
        }
        
        sb.Append(' ');
        sb.Append(EnPassantSquare == Square.None ? "-" : EnPassantSquare.ToAlgebraic());
        sb.Append(' ');
        sb.Append(HalfmoveClock);
        sb.Append(' ');
        sb.Append(FullmoveNumber);
        
        return sb.ToString();
    }

    private static Piece CharToPiece(char c) => c switch
    {
        'P' => Piece.WhitePawn,
        'N' => Piece.WhiteKnight,
        'B' => Piece.WhiteBishop,
        'R' => Piece.WhiteRook,
        'Q' => Piece.WhiteQueen,
        'K' => Piece.WhiteKing,
        'p' => Piece.BlackPawn,
        'n' => Piece.BlackKnight,
        'b' => Piece.BlackBishop,
        'r' => Piece.BlackRook,
        'q' => Piece.BlackQueen,
        'k' => Piece.BlackKing,
        _ => Piece.None
    };

    private static char PieceToChar(Piece piece) => piece switch
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
        _ => ' '
    };


}