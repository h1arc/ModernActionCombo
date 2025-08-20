using Xunit;
using FluentAssertions;
using ModernWrathCombo.Core.Data;

namespace ModernWrathCombo.Tests.Unit.Core;

/// <summary>
/// Unit tests for GameState struct functionality.
/// Tests readonly struct behavior and helper methods.
/// </summary>
public class GameStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var gameState = new GameState(24, 90, true, 1001, 2.5f);

        // Assert
        gameState.JobId.Should().Be(24);
        gameState.Level.Should().Be(90);
        gameState.InCombat.Should().BeTrue();
        gameState.CurrentTarget.Should().Be(1001);
        gameState.GlobalCooldownRemaining.Should().Be(2.5f);
    }

    [Theory]
    [InlineData(0.0f, true)]   // No GCD remaining, can weave
    [InlineData(0.3f, true)]   // Low GCD remaining, can weave
    [InlineData(0.5f, true)]   // Exactly 0.5s, can weave
    [InlineData(0.6f, false)]  // Above 0.5s, cannot weave
    [InlineData(2.5f, false)]  // High GCD remaining, cannot weave
    public void CanUseAbility_ReturnsCorrectValue(float gcdRemaining, bool expected)
    {
        // Arrange
        var gameState = new GameState(24, 90, true, 1001, gcdRemaining);

        // Act
        var result = gameState.CanUseAbility();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, false)]    // No target
    [InlineData(1001u, true)]  // Valid target
    [InlineData(999999u, true)] // Another valid target
    public void IsValidTarget_ReturnsCorrectValue(uint targetId, bool expected)
    {
        // Arrange
        var gameState = new GameState(24, 90, true, targetId, 0.0f);

        // Act
        var result = gameState.IsValidTarget();

        // Assert
        result.Should().Be(expected);
    }
}

/// <summary>
/// Unit tests for StatusEffect struct functionality.
/// </summary>
public class StatusEffectTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var effect = new StatusEffect(1871, 25.5f, 3, 12345);

        // Assert
        effect.Id.Should().Be(1871);
        effect.RemainingDuration.Should().Be(25.5f);
        effect.StackCount.Should().Be(3);
        effect.SourceId.Should().Be(12345);
    }

    [Fact]
    public void Constructor_WithDefaults_UsesCorrectDefaults()
    {
        // Arrange & Act
        var effect = new StatusEffect(1871, 25.5f);

        // Assert
        effect.Id.Should().Be(1871);
        effect.RemainingDuration.Should().Be(25.5f);
        effect.StackCount.Should().Be(1);
        effect.SourceId.Should().Be(0);
    }

    [Theory]
    [InlineData(10.0f, 5.0f, false)]  // Duration > threshold
    [InlineData(5.0f, 5.0f, true)]    // Duration = threshold
    [InlineData(3.0f, 5.0f, true)]    // Duration < threshold
    [InlineData(0.0f, 5.0f, true)]    // Expired
    public void IsExpiringSoon_ReturnsCorrectValue(float duration, float threshold, bool expected)
    {
        // Arrange
        var effect = new StatusEffect(1871, duration);

        // Act
        var result = effect.IsExpiringSoon(threshold);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(10.0f, true)]   // Active
    [InlineData(0.1f, true)]    // Still active
    [InlineData(0.0f, false)]   // Expired
    [InlineData(-1.0f, false)]  // Negative (shouldn't happen but test anyway)
    public void IsActive_ReturnsCorrectValue(float duration, bool expected)
    {
        // Arrange
        var effect = new StatusEffect(1871, duration);

        // Act
        var result = effect.IsActive();

        // Assert
        result.Should().Be(expected);
    }
}

/// <summary>
/// Unit tests for ActionState struct functionality.
/// </summary>
public class ActionStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var action = new ActionState(136, 30.5f, 2, 1);

        // Assert
        action.Id.Should().Be(136);
        action.CooldownRemaining.Should().Be(30.5f);
        action.MaxCharges.Should().Be(2);
        action.CurrentCharges.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithDefaults_UsesCorrectDefaults()
    {
        // Arrange & Act
        var action = new ActionState(136, 30.5f);

        // Assert
        action.Id.Should().Be(136);
        action.CooldownRemaining.Should().Be(30.5f);
        action.MaxCharges.Should().Be(1);
        action.CurrentCharges.Should().Be(1);
    }

    [Theory]
    [InlineData(0.0f, 1u, true)]   // No cooldown, has charge
    [InlineData(10.0f, 1u, false)] // On cooldown
    [InlineData(0.0f, 0u, false)]  // No cooldown but no charges
    [InlineData(10.0f, 0u, false)] // On cooldown and no charges
    public void IsReady_ReturnsCorrectValue(float cooldown, uint charges, bool expected)
    {
        // Arrange
        var action = new ActionState(136, cooldown, 2, charges);

        // Act
        var result = action.IsReady();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0f, false)]   // No cooldown
    [InlineData(0.1f, true)]    // Small cooldown
    [InlineData(30.0f, true)]   // Large cooldown
    public void IsOnCooldown_ReturnsCorrectValue(float cooldown, bool expected)
    {
        // Arrange
        var action = new ActionState(136, cooldown);

        // Act
        var result = action.IsOnCooldown();

        // Assert
        result.Should().Be(expected);
    }
}
