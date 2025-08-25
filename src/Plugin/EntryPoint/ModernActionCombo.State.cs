using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using ModernActionCombo.Core.Services;
using ModernActionCombo.UI.Windows;

namespace ModernActionCombo;

// Holds fields/state previously on the main class
public sealed partial class ModernActionCombo
{
    private ActionInterceptor? _actionInterceptor;
    private SmartTargetInterceptor? _smartTargetInterceptor;

    private JobConfigWindow? _configWindow;
    private MainSettingsWindow? _mainSettingsWindow;
    private bool _initialized = false;

    // Movement detection
    private Vector3 _lastPosition;
    private long _lastPositionUpdate;
    private const float MOVEMENT_THRESHOLD = 0.01f;

    // Change detection
    private uint _lastKnownJob = 0;
    private uint _lastKnownLevel = 0;
    private bool _lastInDuty = false;
    private uint _lastDutyId = 0;
    private bool _lastInCombat = false;
    private uint _lastKnownTargetId = 0;

    // Scratch dictionaries reused each frame to avoid GC
    private readonly Dictionary<uint, float> _scratchBuffs = new(capacity: 32);
    private readonly Dictionary<uint, float> _scratchDebuffs = new(capacity: 32);
    private readonly Dictionary<uint, float> _scratchCooldowns = new(capacity: 64);

    // Reflection cache
    private static readonly Dictionary<Type, PropertyInfo?> _ownerIdPropertyCache = new();

    // Cached cooldown tracking list for current job/provider
    private uint[] _cooldownsToTrack = Array.Empty<uint>();
}
