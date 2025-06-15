namespace Meridian.Core;

using System.Runtime.CompilerServices;

public static class MakeMoveExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MakeMove(ref this BoardState board, Move move)
    {
        var (piece, color) = board.GetPieceAt(move.From);
        
        // Update castling rights
        UpdateCastlingRights(ref board, move, piece);
        
        // Reset en passant
        board.EnPassantSquare = Square.None;
        
        // Update halfmove clock
        if (piece == Piece.Pawn || move.IsCapture())
            board.HalfMoveClock = 0;
        else
            board.HalfMoveClock++;
        
        // Recalculate material if this is a capture
        if (move.IsCapture())
        {
            board.CachedMaterial = board.CalculateMaterial();
        }
        
        // Handle different move types
        switch (move.Type)
        {
            case MoveType.Normal:
                    // Check if this is a quiet promotion
                    if (move.IsPromotion())
                    {
                        board.RemovePiece(move.From, piece, color);
                        board.AddPiece(move.To, move.PromotionPiece, color);
                    }
                    else
                    {
                        board.MovePiece(move.From, move.To, piece, color);
                        
                        // Set en passant square for double pawn push
                        if (piece == Piece.Pawn)
                        {
                            int distance = Math.Abs((int)move.To - (int)move.From);
                            if (distance == 16)
                            {
                                board.EnPassantSquare = (Square)((int)move.From + (color == Color.White ? 8 : -8));
                            }
                        }
                    }
                    break;
                    
            case MoveType.Capture:
                    var (capturedPiece, capturedColor) = board.GetPieceAt(move.To);
                    board.RemovePiece(move.To, capturedPiece, capturedColor);
                    
                    // Check if this is a promotion capture
                    if (move.IsPromotion())
                    {
                        board.RemovePiece(move.From, piece, color);
                        board.AddPiece(move.To, move.PromotionPiece, color);
                    }
                    else
                    {
                        board.MovePiece(move.From, move.To, piece, color);
                    }
                    break;
                    
            case MoveType.Castle:
                    board.MovePiece(move.From, move.To, piece, color);
                    
                    // Move the rook
                    if (move.To == Square.G1)
                    {
                        board.MovePiece(Square.H1, Square.F1, Piece.Rook, Color.White);
                    }
                    else if (move.To == Square.C1)
                    {
                        board.MovePiece(Square.A1, Square.D1, Piece.Rook, Color.White);
                    }
                    else if (move.To == Square.G8)
                    {
                        board.MovePiece(Square.H8, Square.F8, Piece.Rook, Color.Black);
                    }
                    else if (move.To == Square.C8)
                    {
                        board.MovePiece(Square.A8, Square.D8, Piece.Rook, Color.Black);
                    }
                    break;
                    
            case MoveType.EnPassant:
                    board.MovePiece(move.From, move.To, piece, color);
                    
                    // Remove captured pawn
                    Square capturedPawnSquare = (Square)((int)move.To + (color == Color.White ? -8 : 8));
                    board.RemovePiece(capturedPawnSquare, Piece.Pawn, color.Opposite());
                    break;
        }
        
        // Update full move number
        if (board.SideToMove == Color.Black)
            board.FullMoveNumber++;
            
        // Switch side to move
        board.SideToMove = board.SideToMove.Opposite();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateCastlingRights(ref BoardState board, Move move, Piece piece)
    {
        // King moves
        if (piece == Piece.King)
        {
            if (board.SideToMove == Color.White)
                board.CastlingRights &= ~CastlingRights.White;
            else
                board.CastlingRights &= ~CastlingRights.Black;
        }
        
        // Rook moves
        if (piece == Piece.Rook)
        {
            if (move.From == Square.A1)
                board.CastlingRights &= ~CastlingRights.WhiteQueenSide;
            else if (move.From == Square.H1)
                board.CastlingRights &= ~CastlingRights.WhiteKingSide;
            else if (move.From == Square.A8)
                board.CastlingRights &= ~CastlingRights.BlackQueenSide;
            else if (move.From == Square.H8)
                board.CastlingRights &= ~CastlingRights.BlackKingSide;
        }
        
        // Rook captures - only update if an opposing rook is actually captured
        var (capturedPiece, capturedColor) = board.GetPieceAt(move.To);
        if (capturedPiece == Piece.Rook)
        {
            if (capturedColor == Color.White)
            {
                if (move.To == Square.A1)
                    board.CastlingRights &= ~CastlingRights.WhiteQueenSide;
                else if (move.To == Square.H1)
                    board.CastlingRights &= ~CastlingRights.WhiteKingSide;
            }
            else // capturedColor == Color.Black
            {
                if (move.To == Square.A8)
                    board.CastlingRights &= ~CastlingRights.BlackQueenSide;
                else if (move.To == Square.H8)
                    board.CastlingRights &= ~CastlingRights.BlackKingSide;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnmakeMove(ref this BoardState board, Move move, in BoardState previousState)
    {
        // Simply restore the previous state
        board = previousState;
    }
}