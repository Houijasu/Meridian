namespace Meridian;

using System.Runtime.CompilerServices;

using Core;

internal class Program
{
   [ModuleInitializer]
   public static void Initialize()
   {
      Console.Title = nameof(Meridian);
   }

   public static void Main(string[] args)
   {
      Perft.RunPerftSuite();
      // Debug.DebugPosition4.Analyze();
      // Debug.DebugA7Promotion.Analyze();
      // Debug.DebugPawnCaptures.Analyze();
      // Debug.DebugFenParsing.Analyze();
      // Debug.DebugPosition3.RunDebug();
      // Debug.ComparePerft.ComparePosition3();
      // Debug.ListAllMoves.ShowPosition3Moves();
      // Debug.AnalyzePawnMoves.Analyze();
      // Debug.CheckEnPassant.VerifyEnPassant();
      // Debug.DebugKiwipete.Analyze();
      // Debug.DetailedMoveDebug.DebugSpecificMove();
      // Debug.TraceCastling.TraceBlackCastling();
      // Debug.TraceGeneration.TraceBlackMoveGeneration();
      // Debug.TraceMoveEncoding.TestCastlingEncoding();
      // Debug.TestMoveComparison.TestCastleMoveDetection();
      // DebugH6F7.Debug();
      // DebugE8E7.Debug();
      // DebugC4C5.Debug();
      // DebugPosition4.Debug();
      // TestPromotion.Test();
      // Console.WriteLine("\nPress any key to exit...");
      // Console.ReadKey(true);
   }
}
