using System;
using Dalamud.Plugin.Services;
using ModernActionCombo.Core.Interfaces;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Adapter that implements ILogger using Dalamud's IPluginLog.
/// Bridges pure .NET 9 logging interface with Dalamud's logging system.
/// </summary>
public class DalamudLoggerAdapter : ILogger
{
    private readonly IPluginLog _pluginLog;
    
    public DalamudLoggerAdapter(IPluginLog pluginLog)
    {
        _pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
    }
    
    public bool IsDebugEnabled => true; // Dalamud debug logging is always available
    
    public void Debug(string message)
    {
        _pluginLog.Debug(message);
    }
    
    public void Information(string message)
    {
        _pluginLog.Information(message);
    }
    
    public void Warning(string message)
    {
        _pluginLog.Warning(message);
    }
    
    public void Error(string message)
    {
        _pluginLog.Error(message);
    }
    
    public void Error(string message, Exception exception)
    {
        _pluginLog.Error(exception, message);
    }
}
