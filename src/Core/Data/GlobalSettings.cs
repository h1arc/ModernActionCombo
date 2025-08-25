using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Global plugin settings backed by a dedicated JobConfiguration with jobId=0.
/// Provides toggles used across jobs (e.g., companion scanning).
/// </summary>
public static class GlobalSettings
{
    private const uint GLOBAL_JOB_ID = 0u;
    private const string CompanionScanEnabledKey = "CompanionScanEnabled";
    private const string CompanionOverrideEnabledKey = "CompanionOverrideWhenLowerHp";
    private const string CompanionOverrideDeltaKey = "CompanionOverrideHpDelta";

    public static bool CompanionScanEnabled
    {
        get => ConfigurationManager.GetJobConfiguration(GLOBAL_JOB_ID).GetSetting(CompanionScanEnabledKey, true);
        set
        {
            var cfg = ConfigurationManager.GetJobConfiguration(GLOBAL_JOB_ID);
            cfg.SetSetting(CompanionScanEnabledKey, value);
            ConfigAwareActionCache.IncrementConfigVersion();
            ConfigSaveScheduler.NotifyChanged();
        }
    }

    /// <summary>
    /// If enabled, companion can override party priority when its HP% is significantly lower.
    /// Default: disabled.
    /// </summary>
    public static bool CompanionOverrideWhenLowerHp
    {
        get => ConfigurationManager.GetJobConfiguration(GLOBAL_JOB_ID).GetSetting(CompanionOverrideEnabledKey, false);
        set
        {
            var cfg = ConfigurationManager.GetJobConfiguration(GLOBAL_JOB_ID);
            cfg.SetSetting(CompanionOverrideEnabledKey, value);
            ConfigAwareActionCache.IncrementConfigVersion();
            ConfigSaveScheduler.NotifyChanged();
        }
    }

    /// <summary>
    /// The HP% delta required for the companion to override party priority (0..1).
    /// Default: 0.25 (25%).
    /// </summary>
    public static float CompanionOverrideHpDelta
    {
        get => ConfigurationManager.GetJobConfiguration(GLOBAL_JOB_ID).GetSetting(CompanionOverrideDeltaKey, 0.25f);
        set
        {
            var cfg = ConfigurationManager.GetJobConfiguration(GLOBAL_JOB_ID);
            cfg.SetSetting(CompanionOverrideDeltaKey, value);
            ConfigAwareActionCache.IncrementConfigVersion();
            ConfigSaveScheduler.NotifyChanged();
        }
    }
}
