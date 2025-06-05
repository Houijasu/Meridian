namespace Meridian;

using System.IO;

using Core.UCI;

internal class Program
{
   public static void Main(string[] args)
   {
      // Do NOT print any startup message - Fritz expects silence until "uci" command
      // Do NOT set console title - some GUIs might not like it
      
      // Ensure console output is not buffered
      Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) 
      { 
         AutoFlush = true 
      });
      
      // Use UCI implementation
      var uci = new UciProtocol();
      uci.Run();
   }
}
