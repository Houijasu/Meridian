using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeridianDebugWrapper;

/// <summary>
/// Wrapper program that intercepts console I/O between a GUI (like Fritz) and Meridian.exe
/// This allows us to log all communication for debugging purposes.
/// </summary>
public class Program
{
    private static StreamWriter? logWriter;
        private static readonly object logLock = new object();
        
        public static void Main(string[] args)
        {
            // Set up logging
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"meridian_debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            logWriter = new StreamWriter(logPath, false, Encoding.UTF8) { AutoFlush = true };
            
            LogMessage("=== Meridian Wrapper Started ===");
            LogMessage($"Log file: {logPath}");
            LogMessage($"Working directory: {Environment.CurrentDirectory}");
            
            // Path to the actual Meridian.exe
            var meridianPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Meridian_Original.exe");
            if (!File.Exists(meridianPath))
            {
                // Try in parent directory
                meridianPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Meridian.exe");
            }
            
            LogMessage($"Meridian path: {meridianPath}");
            
            if (!File.Exists(meridianPath))
            {
                Console.Error.WriteLine($"Error: Meridian executable not found at {meridianPath}");
                LogMessage($"ERROR: Meridian executable not found!");
                Environment.Exit(1);
            }
            
            try
            {
                // Start the actual Meridian process
                var startInfo = new ProcessStartInfo
                {
                    FileName = meridianPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };
                
                using var meridianProcess = Process.Start(startInfo);
                if (meridianProcess == null)
                {
                    throw new Exception("Failed to start Meridian process");
                }
                
                LogMessage($"Meridian process started with PID: {meridianProcess.Id}");
                
                // Set up cancellation for graceful shutdown
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) => 
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                
                // Start tasks to relay I/O
                var tasks = new Task[]
                {
                    // Relay stdin from GUI to Meridian
                    Task.Run(() => RelayInput(meridianProcess, cts.Token)),
                    
                    // Relay stdout from Meridian to GUI
                    Task.Run(() => RelayOutput(meridianProcess, "STDOUT", meridianProcess.StandardOutput, Console.Out, cts.Token)),
                    
                    // Relay stderr from Meridian to GUI
                    Task.Run(() => RelayOutput(meridianProcess, "STDERR", meridianProcess.StandardError, Console.Error, cts.Token))
                };
                
                // Wait for process to exit or cancellation
                while (!meridianProcess.HasExited && !cts.Token.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
                
                LogMessage("Meridian process exiting...");
                
                // Cancel all tasks
                cts.Cancel();
                
                // Give tasks time to complete
                Task.WaitAll(tasks, TimeSpan.FromSeconds(2));
                
                if (!meridianProcess.HasExited)
                {
                    LogMessage("Terminating Meridian process...");
                    meridianProcess.Kill();
                }
                
                LogMessage($"Meridian process exited with code: {meridianProcess.ExitCode}");
            }
            catch (Exception ex)
            {
                LogMessage($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                Console.Error.WriteLine($"Wrapper error: {ex.Message}");
            }
            finally
            {
                LogMessage("=== Meridian Wrapper Stopped ===");
                logWriter?.Dispose();
            }
        }
        
        private static void RelayInput(Process meridianProcess, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = Console.ReadLine();
                    if (line == null) break;
                    
                    // Log the input
                    LogMessage($"GUI->Engine: {line}");
                    
                    // Send to Meridian
                    meridianProcess.StandardInput.WriteLine(line);
                    meridianProcess.StandardInput.Flush();
                    
                    // Special handling for quit command
                    if (line.Trim().ToLower() == "quit")
                    {
                        LogMessage("Quit command received, stopping input relay");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Input relay error: {ex.Message}");
            }
        }
        
        private static void RelayOutput(Process meridianProcess, string streamName, StreamReader source, TextWriter destination, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && !meridianProcess.HasExited)
                {
                    var line = source.ReadLine();
                    if (line == null) break;
                    
                    // Log the output
                    LogMessage($"Engine->GUI ({streamName}): {line}");
                    
                    // Send to GUI
                    destination.WriteLine(line);
                    destination.Flush();
                    
                    // Check for specific patterns that might indicate issues
                    if (line.Contains("bestmove"))
                    {
                        LogMessage($"BESTMOVE DETECTED: {line}");
                    }
                    else if (line.StartsWith("info string"))
                    {
                        LogMessage($"DEBUG INFO: {line}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Output relay error ({streamName}): {ex.Message}");
            }
        }
        
        private static void LogMessage(string message)
        {
            lock (logLock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";
                
                // Write to log file
                logWriter?.WriteLine(logLine);
                
                // Also write to stderr for debugging (won't interfere with UCI protocol)
                Console.Error.WriteLine(logLine);
            }
        }
    }