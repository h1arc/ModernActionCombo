using System;

namespace ModernActionCombo.Core.Interfaces;

/// <summary>
/// Log severity levels.
/// </summary>
public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

/// <summary>
/// Pure interface for logging.
/// Decouples core logic from Dalamud logging implementation.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Check if debug logging is enabled for performance optimization.
    /// </summary>
    bool IsDebugEnabled { get; }
    
    /// <summary>
    /// Logs a debug message.
    /// </summary>
    void Debug(string message);
    
    /// <summary>
    /// Logs an information message.
    /// </summary>
    void Information(string message);
    
    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void Warning(string message);
    
    /// <summary>
    /// Logs an error message.
    /// </summary>
    void Error(string message);
    
    /// <summary>
    /// Logs an error message with exception details.
    /// </summary>
    void Error(string message, Exception exception);

    /// <summary>
    /// Unified logging entrypoint with a default implementation that maps to level-specific methods.
    /// Implementers can override for structured logging or to avoid double-dispatch.
    /// </summary>
    /// <remarks>
    /// This default body keeps the change non-breaking while enabling a single-call site in callers.
    /// </remarks>
    void Log(LogLevel level, string message, Exception? exception = null)
    {
        switch (level)
        {
            case LogLevel.Debug:
                Debug(message);
                if (exception is not null) Debug(exception.ToString());
                break;
            case LogLevel.Information:
                Information(message);
                if (exception is not null) Information(exception.ToString());
                break;
            case LogLevel.Warning:
                Warning(message);
                if (exception is not null) Warning(exception.ToString());
                break;
            case LogLevel.Error:
                if (exception is not null) Error(message, exception);
                else Error(message);
                break;
            default:
                Information(message);
                if (exception is not null) Information(exception.ToString());
                break;
        }
    }
}
