#nullable enable

using Meridian.Core.Protocol.UCI;

namespace Meridian;

public class Program
{
    public static void Main(string[] args)
    {
        var uciEngine = new UciEngine();
        
        while (true)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;
                
            uciEngine.ProcessCommand(input);
            
            if (input.Trim().ToLowerInvariant() == "quit")
                break;
        }
    }
}