using System;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Global debounced configuration save scheduler.
/// Call NotifyChanged() on any config mutation; call TryFlushIfDue() once per frame.
/// </summary>
public static class ConfigSaveScheduler
{
    private static long _lastChangeTick;
    private static bool _pending;
    private const long DebounceMs = 1000; // 1 second

    public static void NotifyChanged()
    {
        _lastChangeTick = Environment.TickCount64;
        _pending = true;
    }

    public static void TryFlushIfDue()
    {
        if (!_pending) return;
        if (Environment.TickCount64 - _lastChangeTick < DebounceMs) return;

        try
        {
            ConfigurationStorage.SaveAll();
        }
        catch
        {
            // ignore write errors; next frame will retry if another change occurs
        }
        finally
        {
            _pending = false;
        }
    }
}
