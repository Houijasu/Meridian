namespace Meridian.Protocols;

/// <summary>
/// Interface for chess communication protocols (UCI, XBoard, etc.)
/// </summary>
public interface IProtocol
{
    /// <summary>
    /// Name of the protocol (e.g., "UCI", "XBoard")
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Start the protocol loop, handling input/output
    /// </summary>
    /// <param name="sendInitialUciResponse">Whether to send initial UCI response (for when "uci" was already consumed)</param>
    void Run(bool sendInitialUciResponse = false);
    
    /// <summary>
    /// Stop the protocol gracefully
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Check if the protocol is currently running
    /// </summary>
    bool IsRunning { get; }
}