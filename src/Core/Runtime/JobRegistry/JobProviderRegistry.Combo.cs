using System;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Interfaces;

namespace ModernActionCombo.Core.Services;

public static partial class JobProviderRegistry
{
    #region Combo Processing (from JobComboRegistry)
    // Ultra-fast single-dispatch cache for the hot path
    private static Func<uint, GameStateData, uint>? _fastResolver;
    private static uint _cachedJobId = 0;
    private static uint _cachedConfigVersion = 0;

    /// <summary>
    /// Resolve an action using the active job's combo logic.
    /// Ultra-optimized single-dispatch for <20ns resolution with config-aware caching.
    /// </summary>
    public static uint ResolveAction(uint actionId, GameStateData gameState)
    {
        // Global check: If combo processing conditions aren't met, return original action immediately
        if (!GameStateCache.CanProcessCombos)
            return actionId;

        var currentConfigVersion = ConfigAwareActionCache.GetConfigVersion();

        // Ultra-fast path: use cached resolver if job and config haven't changed
        if (_fastResolver != null &&
            gameState.JobId == _cachedJobId &&
            currentConfigVersion == _cachedConfigVersion)
        {
            return _fastResolver(actionId, gameState);
        }

        // Job or config changed - rebuild the fast resolver
        return RebuildFastResolver(actionId, gameState, currentConfigVersion);
    }

    /// <summary>
    /// Rebuilds the ultra-fast resolver when job or configuration changes.
    /// This is the cold path - only called on job/config change.
    /// </summary>
    private static uint RebuildFastResolver(uint actionId, GameStateData gameState, uint configVersion)
    {
        _cachedJobId = gameState.JobId;
        _cachedConfigVersion = configVersion;
        _fastResolver = null;

        if (_activeProvider?.AsComboProvider() is not IComboProvider comboProvider)
        {
            _fastResolver = static (id, state) => id; // No-op resolver
            return actionId;
        }

        try
        {
            var grids = comboProvider.GetComboGrids();
            var hasOGCDSupport = _activeProvider is IOGCDProvider ogcdProvider;

            if (grids.Count == 1 && hasOGCDSupport)
            {
                // Ultra-fast path for single grid + OGCD jobs (like WHM)
                var grid = grids[0];
                _fastResolver = (id, state) =>
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;

                    if (!grid.HandlesAction(id)) return id;

                    var resolvedGCD = grid.Evaluate(id, state);

                    // If GCD changed, return it immediately (higher priority than OGCDs)
                    if (resolvedGCD != id) return resolvedGCD;

                    // If can weave, get first OGCD directly (no LINQ allocation)
                    // BUT: Re-evaluate OGCD conditions dynamically - don't cache the result!
                    if (GameStateCache.CanWeave())
                    {
                        foreach (var ogcd in ((IOGCDProvider)_activeProvider!).GetSuggestedOGCDs())
                        {
                            return ogcd; // Return first valid OGCD (conditions already checked in GetSuggestedOGCDs)
                        }
                    }

                    return resolvedGCD; // No OGCDs available or can't weave
                };
            }
            else if (grids.Count == 1)
            {
                // Fast path for single grid, no OGCD
                var grid = grids[0];
                _fastResolver = (id, state) =>
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;

                    return grid.HandlesAction(id) ? grid.Evaluate(id, state) : id;
                };
            }
            else if (hasOGCDSupport)
            {
                // Multi-grid with OGCD support
                _fastResolver = (id, state) =>
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;

                    foreach (var grid in grids)
                    {
                        if (grid.HandlesAction(id))
                        {
                            var resolvedGCD = grid.Evaluate(id, state);

                            // If GCD changed, return it immediately (higher priority)
                            if (resolvedGCD != id) return resolvedGCD;

                            // If can weave, dynamically check OGCD conditions
                            if (GameStateCache.CanWeave())
                            {
                                foreach (var ogcd in ((IOGCDProvider)_activeProvider!).GetSuggestedOGCDs())
                                {
                                    return ogcd; // Return first valid OGCD
                                }
                            }
                            return resolvedGCD; // No OGCDs available or can't weave
                        }
                    }
                    return id;
                };
            }
            else
            {
                // Multi-grid, no OGCD
                _fastResolver = (id, state) =>
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;

                    foreach (var grid in grids)
                    {
                        if (grid.HandlesAction(id))
                        {
                            return grid.Evaluate(id, state);
                        }
                    }
                    return id;
                };
            }

            // Execute the newly built resolver
            return _fastResolver(actionId, gameState);
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"❌ Error building fast resolver: {ex}");
            _fastResolver = static (id, state) => id; // Fallback to no-op
            return actionId;
        }
    }
    #endregion
}
