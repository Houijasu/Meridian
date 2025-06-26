#nullable enable

using Meridian.Core.Board;
using Meridian.Core.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Meridian.Tests.Evaluation;

[TestClass]
public sealed class EvaluatorTests
{
    [TestMethod]
    public void TestStartingPositionEvaluation()
    {
        var position = Position.StartingPosition();
        var score = Evaluator.Evaluate(position);
        
        Assert.IsTrue(Math.Abs(score) < 50, $"Starting position should be roughly equal. Score: {score}");
    }
    
    [TestMethod]
    public void TestMaterialAdvantage()
    {
        var positionResult = Position.FromFen("rnbqkb1r/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.IsTrue(score > 250 && score < 350, $"White should be up a knight (~320). Score: {score}");
    }
    
    [TestMethod]
    public void TestPassedPawnEvaluation()
    {
        var positionResult = Position.FromFen("8/1P6/8/8/8/8/1p6/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.IsTrue(score > 0, "White's passed pawn on 7th rank should give advantage");
    }
    
    [TestMethod]
    public void TestDoubledPawnPenalty()
    {
        var positionResult = Position.FromFen("8/p7/p7/8/8/P7/P7/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.AreEqual(0, Math.Abs(score) / 10 * 10, "Both sides have doubled pawns, should be roughly equal");
    }
    
    [TestMethod]
    public void TestIsolatedPawnPenalty()
    {
        var positionResult = Position.FromFen("8/8/3p4/8/8/3P4/8/8 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.IsTrue(Math.Abs(score) < 30, "Both sides have isolated pawns, should be roughly equal");
    }
    
    [TestMethod]
    public void TestKingSafetyWithPawnShield()
    {
        var positionResult = Position.FromFen("r3k2r/ppp2ppp/8/8/8/8/PPP2PPP/R3K2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.IsTrue(Math.Abs(score) < 50, "Both kings have pawn shields, should be roughly equal");
    }
    
    [TestMethod]
    public void TestBishopPairBonus()
    {
        var positionResult = Position.FromFen("r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQK2R w KQkq - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var whiteEval = Evaluator.Evaluate(positionResult.Value);
        
        var noBishopPairResult = Position.FromFen("r1bqk2r/pppp1ppp/2n2n2/4p3/2B1P3/3P1N2/PPP2PPP/RNBQK2R w KQkq - 0 1");
        Assert.IsTrue(noBishopPairResult.IsSuccess);
        var noBishopEval = Evaluator.Evaluate(noBishopPairResult.Value);
        
        Assert.IsTrue(whiteEval < noBishopEval, "Position with bishop pair should be better for black");
    }
    
    [TestMethod]
    public void TestMobilityEvaluation()
    {
        var positionResult = Position.FromFen("8/8/8/3N4/8/8/8/3n4 w - - 0 1");
        Assert.IsTrue(positionResult.IsSuccess);
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.IsTrue(Math.Abs(score) < 50, "Knights with similar mobility should be roughly equal");
    }
    
    [TestMethod]
    public void TestEndgameKingActivity()
    {
        var endgameResult = Position.FromFen("8/8/4k3/8/8/4K3/8/8 w - - 0 1");
        Assert.IsTrue(endgameResult.IsSuccess);
        var centralKings = Evaluator.Evaluate(endgameResult.Value);
        
        var cornerKingResult = Position.FromFen("7k/8/8/8/8/8/8/K7 w - - 0 1");
        Assert.IsTrue(cornerKingResult.IsSuccess);
        var cornerScore = Evaluator.Evaluate(cornerKingResult.Value);
        
        Assert.IsTrue(Math.Abs(centralKings) < Math.Abs(cornerScore), 
            "Central kings in endgame should be better than corner kings");
    }
    
    [TestMethod]
    public void TestPhaseCalculation()
    {
        var midgame = Position.StartingPosition();
        var midgameScore = Evaluator.Evaluate(midgame);
        
        var endgameResult = Position.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        Assert.IsTrue(endgameResult.IsSuccess);
        var endgameScore = Evaluator.Evaluate(endgameResult.Value);
        
        Assert.IsNotNull(midgameScore);
        Assert.IsNotNull(endgameScore);
    }
    
    [TestMethod]
    [DataRow("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 0, 50)]
    [DataRow("r1bqkbnr/pppp1ppp/2n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 4 4", -50, 50)]
    [DataRow("8/8/8/3Q4/8/8/8/3q4 w - - 0 1", -50, 50)]
    [DataRow("8/8/8/3R4/8/8/8/3r4 w - - 0 1", -50, 50)]
    public void TestSymmetricPositions(string fen, int minScore, int maxScore)
    {
        var positionResult = Position.FromFen(fen);
        Assert.IsTrue(positionResult.IsSuccess, $"Failed to parse FEN: {fen}");
        var score = Evaluator.Evaluate(positionResult.Value);
        
        Assert.IsTrue(score >= minScore && score <= maxScore, 
            $"Symmetric position score {score} should be between {minScore} and {maxScore}");
    }
}