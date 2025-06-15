namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class FenParser
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static BoardState Parse(ReadOnlySpan<char> fen)
    {
        BoardState board = default;
        int index = 0;
        
        // Parse piece placement
        int rank = 7;
        int file = 0;
        
        while (index < fen.Length && fen[index] != ' ')
        {
            char c = fen[index++];
            
            if (c == '/')
            {
                rank--;
                file = 0;
            }
            else if (char.IsDigit(c))
            {
                file += c - '0';
            }
            else
            {
                Square square = SquareExtensions.MakeSquare((File)file, (Rank)rank);
                
                switch (c)
                {
                    case 'P': board.AddPiece(square, Piece.Pawn, Color.White); break;
                    case 'N': board.AddPiece(square, Piece.Knight, Color.White); break;
                    case 'B': board.AddPiece(square, Piece.Bishop, Color.White); break;
                    case 'R': board.AddPiece(square, Piece.Rook, Color.White); break;
                    case 'Q': board.AddPiece(square, Piece.Queen, Color.White); break;
                    case 'K': board.AddPiece(square, Piece.King, Color.White); break;
                    case 'p': board.AddPiece(square, Piece.Pawn, Color.Black); break;
                    case 'n': board.AddPiece(square, Piece.Knight, Color.Black); break;
                    case 'b': board.AddPiece(square, Piece.Bishop, Color.Black); break;
                    case 'r': board.AddPiece(square, Piece.Rook, Color.Black); break;
                    case 'q': board.AddPiece(square, Piece.Queen, Color.Black); break;
                    case 'k': board.AddPiece(square, Piece.King, Color.Black); break;
                }
                
                file++;
            }
        }
        
        // Skip space
        index++;
        
        // Parse side to move
        board.SideToMove = fen[index] == 'w' ? Color.White : Color.Black;
        index += 2;
        
        // Parse castling rights
        board.CastlingRights = CastlingRights.None;
        while (index < fen.Length && fen[index] != ' ')
        {
            switch (fen[index])
            {
                case 'K': board.CastlingRights |= CastlingRights.WhiteKingSide; break;
                case 'Q': board.CastlingRights |= CastlingRights.WhiteQueenSide; break;
                case 'k': board.CastlingRights |= CastlingRights.BlackKingSide; break;
                case 'q': board.CastlingRights |= CastlingRights.BlackQueenSide; break;
            }
            index++;
        }
        
        // Skip space
        index++;
        
        // Parse en passant square
        if (index < fen.Length && fen[index] != '-')
        {
            int epFile = fen[index] - 'a';
            int epRank = fen[index + 1] - '1';
            board.EnPassantSquare = SquareExtensions.MakeSquare((File)epFile, (Rank)epRank);
            index += 2;
        }
        else
        {
            board.EnPassantSquare = Square.None;
            index += 1;
        }
        
        // Skip space
        if (index < fen.Length && fen[index] == ' ')
            index++;
        
        // Parse halfmove clock
        board.HalfMoveClock = 0;
        while (index < fen.Length && char.IsDigit(fen[index]))
        {
            board.HalfMoveClock = (byte)(board.HalfMoveClock * 10 + (fen[index] - '0'));
            index++;
        }
        
        // Skip space
        if (index < fen.Length && fen[index] == ' ')
            index++;
        
        // Parse fullmove number
        board.FullMoveNumber = 0;
        while (index < fen.Length && char.IsDigit(fen[index]))
        {
            board.FullMoveNumber = (ushort)(board.FullMoveNumber * 10 + (fen[index] - '0'));
            index++;
        }
        
        // Calculate and cache material
        board.CachedMaterial = board.CalculateMaterial();
        
        return board;
    }

    public static string ToFen(ref BoardState board)
    {
        Span<char> result = stackalloc char[128];
        int index = 0;
        
        // Piece placement
        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            
            for (int file = 0; file < 8; file++)
            {
                Square square = SquareExtensions.MakeSquare((File)file, (Rank)rank);
                var (piece, color) = board.GetPieceAt(square);
                
                if (piece == Piece.None)
                {
                    empty++;
                }
                else
                {
                    if (empty > 0)
                    {
                        result[index++] = (char)('0' + empty);
                        empty = 0;
                    }
                    
                    char pieceChar = piece switch
                    {
                        Piece.Pawn => 'p',
                        Piece.Knight => 'n',
                        Piece.Bishop => 'b',
                        Piece.Rook => 'r',
                        Piece.Queen => 'q',
                        Piece.King => 'k',
                        _ => ' '
                    };
                    
                    if (color == Color.White)
                        pieceChar = char.ToUpper(pieceChar);
                    
                    result[index++] = pieceChar;
                }
            }
            
            if (empty > 0)
                result[index++] = (char)('0' + empty);
            
            if (rank > 0)
                result[index++] = '/';
        }
        
        result[index++] = ' ';
        
        // Side to move
        result[index++] = board.SideToMove == Color.White ? 'w' : 'b';
        result[index++] = ' ';
        
        // Castling rights
        if (board.CastlingRights == CastlingRights.None)
        {
            result[index++] = '-';
        }
        else
        {
            if ((board.CastlingRights & CastlingRights.WhiteKingSide) != 0)
                result[index++] = 'K';
            if ((board.CastlingRights & CastlingRights.WhiteQueenSide) != 0)
                result[index++] = 'Q';
            if ((board.CastlingRights & CastlingRights.BlackKingSide) != 0)
                result[index++] = 'k';
            if ((board.CastlingRights & CastlingRights.BlackQueenSide) != 0)
                result[index++] = 'q';
        }
        
        result[index++] = ' ';
        
        // En passant square
        if (board.EnPassantSquare == Square.None)
        {
            result[index++] = '-';
        }
        else
        {
            result[index++] = (char)('a' + (int)board.EnPassantSquare.GetFile());
            result[index++] = (char)('1' + (int)board.EnPassantSquare.GetRank());
        }
        
        result[index++] = ' ';
        
        // Halfmove clock
        if (board.HalfMoveClock >= 10)
            result[index++] = (char)('0' + board.HalfMoveClock / 10);
        result[index++] = (char)('0' + board.HalfMoveClock % 10);
        
        result[index++] = ' ';
        
        // Fullmove number
        int moves = board.FullMoveNumber;
        if (moves >= 100)
        {
            result[index++] = (char)('0' + moves / 100);
            moves %= 100;
        }
        if (moves >= 10)
        {
            result[index++] = (char)('0' + moves / 10);
            moves %= 10;
        }
        result[index++] = (char)('0' + moves);
        
        return new string(result[..index]);
    }
}