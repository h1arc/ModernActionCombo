using Xunit;
using FluentAssertions;
using ModernWrathCombo.Core.Services;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Jobs.WHM;

namespace ModernWrathCombo.Tests.Integration;

/// <summary>
/// Integration tests for the complete action resolution system.
/// Tests end-to-end scenarios with real game state.
/// </summary>
public class ActionResolutionIntegrationTests
{
    [Fact]
    public void CompleteWHMRotation_FollowsPriorityCorrectly()
    {
        // Arrange
        var resolver = new ActionResolver();
        var whmCombo = new WHMBasicCombo();
        resolver.RegisterHandler(WHMConstants.Glare3, whmCombo);

        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);

        // Scenario 1: No Dia on target -> Should apply Dia
        var noEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        var result1 = resolver.Resolve(WHMConstants.Glare3, gameState, noEffects, playerEffects, actionStates);
        result1.Should().Be(WHMConstants.Dia, "should apply Dia when missing");

        // Scenario 2: Dia applied, PoM ready, can weave -> Should use PoM
        Span<StatusEffect> targetWithDia = stackalloc StatusEffect[1];
        targetWithDia[0] = new StatusEffect(WHMConstants.DiaDebuffId, 25.0f);
        
        Span<ActionState> pomReady = stackalloc ActionState[1];
        pomReady[0] = new ActionState(WHMConstants.PresenceOfMind, 0.0f);
        
        var gameStateCanWeave = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.3f);

        var result2 = resolver.Resolve(WHMConstants.Glare3, gameStateCanWeave, 
            targetWithDia, playerEffects, pomReady);
        result2.Should().Be(WHMConstants.PresenceOfMind, "should use PoM when ready and can weave");

        // Scenario 3: Dia good, PoM on cooldown, Sacred Sight up -> Should use Glare4
        Span<StatusEffect> playerWithSacredSight = stackalloc StatusEffect[1];
        playerWithSacredSight[0] = new StatusEffect(WHMConstants.SacredSightBuffId, 15.0f);
        
        Span<ActionState> pomOnCooldown = stackalloc ActionState[1];
        pomOnCooldown[0] = new ActionState(WHMConstants.PresenceOfMind, 90.0f);

        var result3 = resolver.Resolve(WHMConstants.Glare3, gameState, 
            targetWithDia, playerWithSacredSight, pomOnCooldown);
        result3.Should().Be(WHMConstants.Glare4, "should use Glare4 with Sacred Sight");

        // Scenario 4: Dia good, PoM on cooldown, no Sacred Sight -> Should use Glare3
        var result4 = resolver.Resolve(WHMConstants.Glare3, gameState, 
            targetWithDia, playerEffects, pomOnCooldown);
        result4.Should().Be(WHMConstants.Glare3, "should use Glare3 as filler");

        // Scenario 5: Dia expiring -> Should refresh Dia (overrides Sacred Sight)
        Span<StatusEffect> targetDiaExpiring = stackalloc StatusEffect[1];
        targetDiaExpiring[0] = new StatusEffect(WHMConstants.DiaDebuffId, 3.0f);

        var result5 = resolver.Resolve(WHMConstants.Glare3, gameState, 
            targetDiaExpiring, playerWithSacredSight, pomOnCooldown);
        result5.Should().Be(WHMConstants.Dia, "should refresh Dia even with Sacred Sight");
    }

    [Fact]
    public void MultipleActionHandlers_WorkIndependently()
    {
        // Arrange
        var resolver = new ActionResolver();
        var whmCombo = new WHMBasicCombo();
        var mockCombo = new MockActionHandler(88888);

        resolver.RegisterHandler(WHMConstants.Glare3, whmCombo);
        resolver.RegisterHandler(12345, mockCombo);

        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        var noEffects = ReadOnlySpan<StatusEffect>.Empty;
        var noActions = ReadOnlySpan<ActionState>.Empty;

        // Act & Assert
        var whmResult = resolver.Resolve(WHMConstants.Glare3, gameState, noEffects, noEffects, noActions);
        whmResult.Should().Be(WHMConstants.Dia, "WHM handler should work normally");

        var mockResult = resolver.Resolve(12345, gameState, noEffects, noEffects, noActions);
        mockResult.Should().Be(88888, "mock handler should work independently");

        var unhandledResult = resolver.Resolve(99999, gameState, noEffects, noEffects, noActions);
        unhandledResult.Should().Be(99999, "unhandled actions should pass through");
    }

    [Fact]
    public void BulkResolution_HandlesComplexScenarios()
    {
        // Arrange
        var resolver = new ActionResolver();
        var whmCombo = new WHMBasicCombo();
        resolver.RegisterHandler(WHMConstants.Glare3, whmCombo);

        // Scenario: Missing Dia
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;
        
        var actions = new uint[] { WHMConstants.Glare3, 99999, WHMConstants.Glare3 };
        var expected = new uint[] { WHMConstants.Dia, 99999, WHMConstants.Dia };

        Span<uint> results = stackalloc uint[actions.Length];
        
        // Act
        resolver.ResolveBatch(actions, results, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        for (int i = 0; i < expected.Length; i++)
        {
            results[i].Should().Be(expected[i], 
                $"bulk resolution should handle scenario correctly at index {i}");
        }
    }

    [Fact]
    public void DifferentJobIds_DoNotInterfere()
    {
        // Arrange
        var resolver = new ActionResolver();
        var whmCombo = new WHMBasicCombo();
        resolver.RegisterHandler(WHMConstants.Glare3, whmCombo);

        var noEffects = ReadOnlySpan<StatusEffect>.Empty;
        var noActions = ReadOnlySpan<ActionState>.Empty;

        // Test different job IDs
        var jobs = new uint[] { 1, 19, 20, 21, 22, 23, 25, 26, 27, 28 }; // Various non-WHM jobs

        foreach (var jobId in jobs)
        {
            var gameState = new GameState(jobId, 90, true, 1001, 0.0f);
            
            // Act
            var result = resolver.Resolve(WHMConstants.Glare3, gameState, noEffects, noEffects, noActions);
            
            // Assert
            result.Should().Be(WHMConstants.Glare3, 
                $"job {jobId} should not trigger WHM combo logic");
        }
    }

    /// <summary>
    /// Mock action handler for testing purposes.
    /// </summary>
    private class MockActionHandler : IActionHandler
    {
        private readonly uint _returnValue;

        public MockActionHandler(uint returnValue)
        {
            _returnValue = returnValue;
        }

        public uint Execute(uint originalActionId) => _returnValue;

        public uint Execute(uint originalActionId, GameStateData gameState, ReadOnlySpan<StatusEffect> targetEffects, 
                          ReadOnlySpan<StatusEffect> playerEffects, ReadOnlySpan<ActionState> actionStates) => _returnValue;
    }
}
