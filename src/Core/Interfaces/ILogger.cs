using System;

namespace ModernActionCombo.Core.Interfaces;

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
}
