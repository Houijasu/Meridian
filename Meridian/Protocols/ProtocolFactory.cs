namespace Meridian.Protocols;

using System;
using Meridian.Core;

/// <summary>
/// Factory for creating protocol instances
/// </summary>
public static class ProtocolFactory
{
    public static IProtocol Create(string protocolName, Engine engine, Search search)
    {
        return protocolName?.ToLowerInvariant() switch
        {
            "uci" => new UciProtocol(engine, search),
            // Future: "xboard" => new XBoardProtocol(engine, search),
            _ => throw new NotSupportedException($"Protocol '{protocolName}' is not supported")
        };
    }
    
    public static IProtocol CreateDefault(Engine engine, Search search)
    {
        return new UciProtocol(engine, search);
    }
}