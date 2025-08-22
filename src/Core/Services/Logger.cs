using System;
using ModernActionCombo.Core.Interfaces;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Pure .NET 9 logger implementation.
/// Completely decoupled from any external dependencies.
/// Uses dependency injection for provider-agnostic logging.
/// </summary>
public static class Logger
{
    private static ILogger? _logger;
    private static bool _isDebugEnabled = false;
    
    /// <summary>
    /// Initialize the logger with a specific implementation.
    /// Call this once at startup to set the logging provider.
    /// </summary>
    public static void Initialize(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isDebugEnabled = _logger.IsDebugEnabled;
    }
    
    /// <summary>Check if debug logging is enabled</summary>
    public static bool IsDebugEnabled => _isDebugEnabled;
    
    /// <summary>Log debug message (only if debug is enabled)</summary>
    public static void Debug(string message)
    {
        if (_isDebugEnabled && _logger != null)
        {
            _logger.Debug(message);
        }
    }
    
    /// <summary>Log informational message</summary>
    public static void Information(string message)
    {
        _logger?.Information(message);
    }
    
    /// <summary>Log warning message</summary>
    public static void Warning(string message)
    {
        _logger?.Warning(message);
    }
    
    /// <summary>Log error message</summary>
    public static void Error(string message)
    {
        _logger?.Error(message);
    }
    
    /// <summary>Log error with exception details</summary>
    public static void Error(string message, Exception exception)
    {
        _logger?.Error(message, exception);
    }
}
