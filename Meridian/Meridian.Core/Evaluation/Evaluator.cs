using Meridian.Core.Board;
using Meridian.Core.NNUE;

namespace Meridian.Core.Evaluation;

public static class Evaluator
{
    private static readonly NNUENetwork _nnue = new();
    private static bool _useNNUE;

    public static bool UseNNUE
    {
        get => _useNNUE;
        set => _useNNUE = value && _nnue.IsLoaded;
    }

    public static bool LoadNNUE(string path)
    {
        if (_nnue.LoadNetwork(path))
        {
            _useNNUE = true;
            return true;
        }
        return false;
    }

    public static void InitializeNNUE(Position position)
    {
        if (_useNNUE)
        {
            _nnue.InitializeAccumulator(position);
        }
    }

    public static void UpdateNNUE(Position position, Move move)
    {
        if (_useNNUE)
        {
            _nnue.UpdateAccumulator(position, move);
        }
    }

    public static int Evaluate(Position position)
    {
        if (position == null) return 0;

        if (_useNNUE)
        {
            // NNUE evaluation with safety checks
            var nnueScore = _nnue.Evaluate(position);

            // Safety check: disable NNUE if evaluation values are unrealistic
            if (Math.Abs(nnueScore) > 5000)
            {
                Console.WriteLine($"NNUE: Unrealistic evaluation {nnueScore}, disabling NNUE");
                _useNNUE = false;
                return 0; // Return draw score if NNUE fails
            }

            return nnueScore;
        }

        // No NNUE available - return draw score
        Console.WriteLine("Warning: No evaluation method available, returning draw score");
        return 0;
    }
}
