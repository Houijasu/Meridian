#nullable enable

namespace Meridian.Core.Board;

[Flags]
public enum CastlingRights
{
    None = 0,
    WhiteKingside = 1 << 0,
    WhiteQueenside = 1 << 1,
    BlackKingside = 1 << 2,
    BlackQueenside = 1 << 3,
    
    White = WhiteKingside | WhiteQueenside,
    Black = BlackKingside | BlackQueenside,
    All = White | Black
}