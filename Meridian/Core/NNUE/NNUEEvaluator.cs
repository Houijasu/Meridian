namespace Meridian.Core.NNUE;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// NNUE-based position evaluator using PlentyChess network
/// </summary>
public sealed class NNUEEvaluator
{
    private PlentyNetwork _network;
    private PlentyAccumulator _accumulator;
    private bool _isInitialized;
    
    /// <summary>
    /// Initialize evaluator
    /// </summary>
    public bool Initialize()
    {
        try
        {
            // Try to load embedded resource first
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Meridian.Resources.main.bin";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    var data = new byte[stream.Length];
                    stream.ReadExactly(data);
                    
                    // Write to temporary file and use LoadFromFile
                    string tempPath = Path.GetTempFileName();
                    try
                    {
                        File.WriteAllBytes(tempPath, data);
                        if (!PlentyNetwork.LoadFromFile(tempPath, out _network))
                        {
                            Console.WriteLine("Failed to load network from embedded resource");
                            return false;
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    
                    _isInitialized = true;
                    
                    return true;
                }
            }
            
            // Fallback: try to load from file
            string networkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "main.bin");
            if (File.Exists(networkPath))
            {
                if (PlentyNetwork.LoadFromFile(networkPath, out _network))
                {
                    _isInitialized = true;
                    return true;
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Set position for evaluation
    /// </summary>
    public void SetPosition(ref BoardState board)
    {
        _accumulator.Initialize(ref _network, ref board);
    }
    
    /// <summary>
    /// Make a move (incremental update)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MakeMove(ref BoardState board, Move move)
    {
        _accumulator.Push();
        _accumulator.UpdateAccumulator(ref _network, ref board, move);
    }
    
    /// <summary>
    /// Make null move (just push state, no update)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MakeNullMove()
    {
        _accumulator.Push();
        // No update needed for null move - accumulator stays the same
    }
    
    /// <summary>
    /// Unmake a move
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnmakeMove()
    {
        _accumulator.Pop();
    }
    
    /// <summary>
    /// Evaluate current position
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Evaluate(ref BoardState board)
    {
        if (!_isInitialized)
        {
            // Fallback to classical evaluation
            return Evaluation.Evaluate(ref board);
        }
        
        // Get king positions
        int wKingSquare = Bitboard.BitScanForward(board.WhiteKing);
        int bKingSquare = Bitboard.BitScanForward(board.BlackKing);
        
        // Get raw evaluation
        int eval = _network.Evaluate(ref _accumulator, board.SideToMove, wKingSquare, bKingSquare);
        
        // Already returns from side to move perspective
        return eval;
    }
    
    /// <summary>
    /// Evaluate a position from scratch (no incremental update)
    /// </summary>
    public int EvaluatePosition(ref BoardState board)
    {
        if (!_isInitialized)
        {
            // Fallback to classical evaluation
            return Evaluation.Evaluate(ref board);
        }
        
        _accumulator.Initialize(ref _network, ref board);
        return Evaluate(ref board);
    }
}