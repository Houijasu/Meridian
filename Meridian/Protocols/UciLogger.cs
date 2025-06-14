namespace Meridian.Protocols;

using System;
using System.IO;

/// <summary>
/// Simple thread-safe logger for UCI protocol debugging
/// </summary>
public sealed class UciLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    
    public UciLogger()
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"meridian_uci_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        _writer = new StreamWriter(logPath, append: false) { AutoFlush = true };
        
        LogInfo($"UCI Logger started - PID: {Environment.ProcessId}");
        LogInfo($"Executable: {Environment.ProcessPath}");
        LogInfo($"Working Directory: {Environment.CurrentDirectory}");
        LogInfo($"Command Line: {Environment.CommandLine}");
    }
    
    public void LogInput(string? input)
    {
        Log("IN ", input ?? "<null>");
    }
    
    public void LogOutput(string output)
    {
        Log("OUT", output);
    }
    
    public void LogInfo(string message)
    {
        Log("INF", message);
    }
    
    public void LogError(string message)
    {
        Log("ERR", message);
    }
    
    public void LogSearch(string message)
    {
        Log("SCH", message);
    }
    
    private void Log(string prefix, string message)
    {
        lock (_lock)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _writer.WriteLine($"[{timestamp}] [{prefix}] {message}");
        }
    }
    
    public void Dispose()
    {
        lock (_lock)
        {
            LogInfo("UCI Logger closing");
            _writer.Dispose();
        }
    }
}