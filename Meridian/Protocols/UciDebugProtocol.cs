namespace Meridian.Protocols;

using System;
using System.IO;
using Core;

/// <summary>
/// Debug wrapper for UCI protocol that logs all communication
/// </summary>
public sealed class UciDebugProtocol : IProtocol
{
    private readonly UciProtocol _innerProtocol;
    private readonly StreamWriter _logWriter;
    private readonly TextWriter _originalOut;
    private readonly TextReader _originalIn;
    
    public UciDebugProtocol(Engine engine, Search search)
    {
        _innerProtocol = new UciProtocol(engine, search);
        
        // Create log file with timestamp
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"uci_debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        
        _originalOut = Console.Out;
        _originalIn = Console.In;
        
        // Log startup
        _logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] UCI Debug Protocol Started");
        _logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Executable: {Environment.ProcessPath}");
        _logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Working Directory: {Environment.CurrentDirectory}");
        _logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Command Line: {Environment.CommandLine}");
    }
    
    public string Name => "UCI-Debug";
    public bool IsRunning => _innerProtocol.IsRunning;
    
    public void Run(bool sendInitialUciResponse = false)
    {
        // Intercept Console I/O
        Console.SetOut(new LoggingTextWriter(_originalOut, _logWriter, "OUT"));
        Console.SetIn(new LoggingTextReader(_originalIn, _logWriter));
        
        try
        {
            _innerProtocol.Run(sendInitialUciResponse);
        }
        finally
        {
            // Restore original I/O
            Console.SetOut(_originalOut);
            Console.SetIn(_originalIn);
            _logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] UCI Debug Protocol Stopped");
            _logWriter.Dispose();
        }
    }
    
    public void Stop()
    {
        _innerProtocol.Stop();
    }
    
    private class LoggingTextWriter(TextWriter innerWriter, StreamWriter logWriter, string prefix) : TextWriter
    {
        public override System.Text.Encoding Encoding => innerWriter.Encoding;
        
        public override void WriteLine(string? value)
        {
            logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {prefix}: {value}");
            innerWriter.WriteLine(value);
        }
        
        public override void Write(string? value)
        {
            innerWriter.Write(value);
        }
        
        public override void Flush()
        {
            logWriter.Flush();
            innerWriter.Flush();
        }
    }
    
    private class LoggingTextReader(TextReader innerReader, StreamWriter logWriter) : TextReader
    {
        public override string? ReadLine()
        {
            var line = innerReader.ReadLine();
            logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] IN: {line ?? "<EOF>"}");
            return line;
        }
    }
}