using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace ModernWrathCombo.Core.Services;

/// <summary>
/// A throttled logger that prevents spam by suppressing duplicate messages within a time window.
/// Useful for combat rotations where the same debug message might be logged many times per second.
/// Initialize once with the plugin log instance.
/// </summary>
public static class Logger
{
    private static readonly ConcurrentDictionary<string, DateTime> _lastLogTimes = new();
    private static readonly TimeSpan _defaultThrottleWindow = TimeSpan.FromSeconds(2.0);
    private static IPluginLog? _log;
    
    /// <summary>
    /// Initialize the logger with the plugin log instance.
    /// Call this once during plugin startup.
    /// </summary>
    public static void Initialize(IPluginLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }
    
    /// <summary>
    /// Logs a debug message only if it hasn't been logged recently.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="throttleWindow">How long to suppress duplicates (default: 2 seconds)</param>
    public static void Debug(string message, TimeSpan? throttleWindow = null)
    {
        if (_log == null) return; // Not initialized
        LogIfNotThrottled(msg => _log.Debug(msg), message, throttleWindow ?? _defaultThrottleWindow);
    }
    
    /// <summary>
    /// Logs an info message only if it hasn't been logged recently.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="throttleWindow">How long to suppress duplicates (default: 2 seconds)</param>
    public static void Information(string message, TimeSpan? throttleWindow = null)
    {
        if (_log == null) return; // Not initialized
        LogIfNotThrottled(msg => _log.Information(msg), message, throttleWindow ?? _defaultThrottleWindow);
    }
    
    /// <summary>
    /// Logs a warning message only if it hasn't been logged recently.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="throttleWindow">How long to suppress duplicates (default: 2 seconds)</param>
    public static void Warning(string message, TimeSpan? throttleWindow = null)
    {
        if (_log == null) return; // Not initialized
        LogIfNotThrottled(msg => _log.Warning(msg), message, throttleWindow ?? _defaultThrottleWindow);
    }
    
    /// <summary>
    /// Always logs error messages (errors should not be throttled).
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Error(string message)
    {
        _log?.Error(message);
    }
    
    /// <summary>
    /// Always logs error messages with exceptions (errors should not be throttled).
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="message">The message to log</param>
    public static void Error(Exception exception, string message)
    {
        _log?.Error(exception, message);
    }
    
    /// <summary>
    /// Generic throttled logging implementation.
    /// </summary>
    private static void LogIfNotThrottled(Action<string> logAction, string message, TimeSpan throttleWindow)
    {
        var now = DateTime.UtcNow;
        var key = message; // Use the full message as the key
        
        // Check if we've logged this message recently
        if (_lastLogTimes.TryGetValue(key, out var lastLogTime))
        {
            if (now - lastLogTime < throttleWindow)
            {
                // Still within throttle window, suppress this log
                return;
            }
        }
        
        // Log the message and update the timestamp
        logAction(message);
        _lastLogTimes[key] = now;
        
        // Periodic cleanup to prevent memory bloat (remove entries older than 5 minutes)
        if (_lastLogTimes.Count > 100 && now.Second == 0) // Only cleanup once per minute
        {
            CleanupOldEntries(now);
        }
    }
    
    /// <summary>
    /// Removes old log entries to prevent memory bloat.
    /// </summary>
    private static void CleanupOldEntries(DateTime now)
    {
        var cutoff = now.AddMinutes(-5);
        var keysToRemove = new List<string>();
        
        foreach (var kvp in _lastLogTimes)
        {
            if (kvp.Value < cutoff)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            _lastLogTimes.TryRemove(key, out _);
        }
    }
}
