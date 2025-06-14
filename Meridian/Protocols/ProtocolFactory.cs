namespace Meridian.Protocols;

using Core;

/// <summary>
/// Factory for creating protocol instances
/// </summary>
public static class ProtocolFactory
{
    public static IProtocol? Create(string? protocolName, Engine engine, Search search)
    {
        return protocolName?.ToLowerInvariant() switch
        {
            "uci" => new UciProtocol(engine, search),
            // Future: "xboard" => new XBoardProtocol(engine, search),
            _ => null // Return null instead of throwing
        };
    }
    
    public static IProtocol CreateDefault(Engine engine, Search search)
    {
        return new UciProtocol(engine, search);
    }
}