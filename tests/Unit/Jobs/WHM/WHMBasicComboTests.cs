using Xunit;
using FluentAssertions;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Jobs.WHM;

namespace ModernWrathCombo.Tests.Unit.Jobs.WHM;

/// <summary>
/// Unit tests for WHM basic combo logic.
/// Tests the 4-priority system: Dia → PoM → Glare4 → Glare3
/// </summary>
public class WHMBasicComboTests
{
    private readonly WHMBasicCombo _combo = new();

    [Fact]
    public void Execute_WrongJob_ReturnsOriginalAction()
    {
        // Arrange
        var gameState = new GameState(20, 90, true, 1001, 0.0f); // Not WHM (24)
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Glare3);
    }

    [Fact]
    public void Execute_NotInCombat_ReturnsOriginalAction()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, false, 1001, 0.0f);
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Glare3);
    }

    [Fact]
    public void Execute_NoTarget_ReturnsOriginalAction()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 0, 0.0f);
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Glare3);
    }

    [Fact]
    public void Execute_WrongAction_ReturnsOriginalAction()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = _combo.Execute(12345, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(12345);
    }

    [Fact]
    public void Execute_DiaMissing_ReturnsDia()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty; // No Dia
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Dia);
    }

    [Fact]
    public void Execute_DiaExpiringSoon_ReturnsDia()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        
        Span<StatusEffect> targetEffectsArray = stackalloc StatusEffect[1];
        targetEffectsArray[0] = new StatusEffect(WHMConstants.DiaDebuffId, 3.0f); // Expiring in 3s
        ReadOnlySpan<StatusEffect> targetEffects = targetEffectsArray;
        
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Dia);
    }

    [Fact]
    public void Execute_DiaGood_PoMReady_CanUseAbility_ReturnsPoM()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.3f); // Can use ability
        
        Span<StatusEffect> targetEffectsArray = stackalloc StatusEffect[1];
        targetEffectsArray[0] = new StatusEffect(WHMConstants.DiaDebuffId, 20.0f); // Dia is good
        ReadOnlySpan<StatusEffect> targetEffects = targetEffectsArray;
        
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        
        Span<ActionState> actionStatesArray = stackalloc ActionState[1];
        actionStatesArray[0] = new ActionState(WHMConstants.PresenceOfMind, 0.0f); // PoM ready
        ReadOnlySpan<ActionState> actionStates = actionStatesArray;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.PresenceOfMind);
    }

    [Fact]
    public void Execute_DiaGood_PoMReady_CannotUseAbility_ReturnsGlare3()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 2.0f); // Cannot use ability (GCD too high)
        
        Span<StatusEffect> targetEffectsArray = stackalloc StatusEffect[1];
        targetEffectsArray[0] = new StatusEffect(WHMConstants.DiaDebuffId, 20.0f); // Dia is good
        ReadOnlySpan<StatusEffect> targetEffects = targetEffectsArray;
        
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        
        Span<ActionState> actionStatesArray = stackalloc ActionState[1];
        actionStatesArray[0] = new ActionState(WHMConstants.PresenceOfMind, 0.0f); // PoM ready
        ReadOnlySpan<ActionState> actionStates = actionStatesArray;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Glare3);
    }

    [Fact]
    public void Execute_DiaGood_PoMOnCooldown_SacredSightActive_ReturnsGlare4()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        
        Span<StatusEffect> targetEffectsArray = stackalloc StatusEffect[1];
        targetEffectsArray[0] = new StatusEffect(WHMConstants.DiaDebuffId, 20.0f); // Dia is good
        ReadOnlySpan<StatusEffect> targetEffects = targetEffectsArray;
        
        Span<StatusEffect> playerEffectsArray = stackalloc StatusEffect[1];
        playerEffectsArray[0] = new StatusEffect(WHMConstants.SacredSightBuffId, 10.0f); // Sacred Sight active
        ReadOnlySpan<StatusEffect> playerEffects = playerEffectsArray;
        
        Span<ActionState> actionStatesArray = stackalloc ActionState[1];
        actionStatesArray[0] = new ActionState(WHMConstants.PresenceOfMind, 60.0f); // PoM on cooldown
        ReadOnlySpan<ActionState> actionStates = actionStatesArray;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Glare4);
    }

    [Fact]
    public void Execute_DiaGood_PoMOnCooldown_NoSacredSight_ReturnsGlare3()
    {
        // Arrange
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        
        Span<StatusEffect> targetEffectsArray = stackalloc StatusEffect[1];
        targetEffectsArray[0] = new StatusEffect(WHMConstants.DiaDebuffId, 20.0f); // Dia is good
        ReadOnlySpan<StatusEffect> targetEffects = targetEffectsArray;
        
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty; // No Sacred Sight
        
        Span<ActionState> actionStatesArray = stackalloc ActionState[1];
        actionStatesArray[0] = new ActionState(WHMConstants.PresenceOfMind, 60.0f); // PoM on cooldown
        ReadOnlySpan<ActionState> actionStates = actionStatesArray;

        // Act
        var result = _combo.Execute(WHMConstants.Glare3, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(WHMConstants.Glare3);
    }

    [Fact]
    public void Execute_LegacyInterface_ReturnsOriginalAction()
    {
        // Act
        var result = _combo.Execute(WHMConstants.Glare3);

        // Assert
        result.Should().Be(WHMConstants.Glare3);
    }
}
