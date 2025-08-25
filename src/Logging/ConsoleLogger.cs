using System;
using ModernActionCombo.Core.Interfaces;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Simple console logger implementation for testing.
/// Completely pure .NET 9 with no external dependencies.
/// </summary>
public class ConsoleLogger : ILogger
{
    private readonly bool _debugEnabled;
    
    public ConsoleLogger(bool debugEnabled = false)
    {
        _debugEnabled = debugEnabled;
    }
    
    public bool IsDebugEnabled => _debugEnabled;
    
    public void Debug(string message)
    {
        if (_debugEnabled)
        {
            Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} {message}");
        }
    }
    
    public void Information(string message)
    {
        Console.WriteLine($"[INFO]  {DateTime.Now:HH:mm:ss.fff} {message}");
    }
    
    public void Warning(string message)
    {
        Console.WriteLine($"[WARN]  {DateTime.Now:HH:mm:ss.fff} {message}");
    }
    
    public void Error(string message)
    {
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
    }
    
    public void Error(string message, Exception exception)
    {
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}: {exception}");
    }
}
