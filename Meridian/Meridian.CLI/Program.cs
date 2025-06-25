#nullable enable

using CSharpFunctionalExtensions;
using Meridian.Core.Board;
using Meridian.Core.Protocol.UCI;

namespace Meridian.CLI;

public class Program
{
    private static Position? _currentPosition;
    
    public static void Main(string[] args)
    {
        UciOutput.Info("Meridian Chess Engine v0.1");
        UciOutput.Info("Author: Meridian Team");
        
        while (true)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;
                
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;
                
            var command = parts[0].ToLowerInvariant();
            
            switch (command)
            {
                case "uci":
                    HandleUci();
                    break;
                    
                case "isready":
                    UciOutput.ReadyOk();
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
                    
                case "quit":
                    return;
                    
                default:
                    UciOutput.Error($"Unknown command: {command}");
                    break;
            }
        }
    }
    
    private static void HandleUci()
    {
        UciOutput.Id("Meridian v0.1", "Meridian Team");
        UciOutput.Option("Hash", "spin", "128", "1", "16384");
        UciOutput.Option("Threads", "spin", "1", "1", "128");
        UciOutput.UciOk();
    }
    
    private static void HandleNewGame()
    {
        _currentPosition = null;
        UciOutput.Info("New game started");
    }
    
    private static void HandlePosition(string[] parts)
    {
        if (parts.Length < 2)
        {
            UciOutput.Error("Invalid position command");
            return;
        }
        
        if (parts[1] == "startpos")
        {
            var result = Position.FromFen(Position.StartingFen);
            result.Match(
                onSuccess: pos => 
                {
                    _currentPosition = pos;
                    UciOutput.Info("Position set to starting position");
                },
                onFailure: error => UciOutput.Error(error)
            );
        }
        else if (parts[1] == "fen" && parts.Length >= 8)
        {
            var fen = string.Join(" ", parts.Skip(2).Take(6));
            var result = Position.FromFen(fen);
            result.Match(
                onSuccess: pos => 
                {
                    _currentPosition = pos;
                    UciOutput.Info($"Position set from FEN: {fen}");
                },
                onFailure: error => UciOutput.Error($"Invalid FEN: {error}")
            );
        }
        else
        {
            UciOutput.Error("Invalid position command format");
        }
    }
    
    private static void HandleGo(string[] parts)
    {
        if (_currentPosition == null)
        {
            UciOutput.Error("No position set");
            return;
        }
        
        UciOutput.Info("Searching...");
        UciOutput.BestMove("e2e4");
    }
}