using Xunit;
using FluentAssertions;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Unit.Core;

/// <summary>
/// Tests for basic GameStateData operations.
/// Simplified to avoid ref struct collection issues.
/// </summary>
public class JobRotationTests
{
    [Fact]
    public void GameStateData_Construction_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var gameState = new GameStateData(24, 90, true, 12345, 2.5f);
        
        // Assert
        gameState.JobId.Should().Be(24u);
        gameState.Level.Should().Be(90u);
        gameState.InCombat.Should().BeTrue();
        gameState.CurrentTarget.Should().Be(12345u);
        gameState.GlobalCooldownRemaining.Should().Be(2.5f);
    }
    
    [Fact]
    public void IsValidTarget_ReturnsCorrectValue()
    {
        // Arrange
        var validTarget = new GameStateData(24, 90, true, 12345, 2.5f);
        var noTarget = new GameStateData(24, 90, true, 0, 2.5f);
        
        // Act & Assert
        validTarget.IsValidTarget().Should().BeTrue();
        noTarget.IsValidTarget().Should().BeFalse();
    }
    
    [Fact]
    public void CanUseAbility_ReturnsCorrectValue()
    {
        // Arrange
        var canWeave = new GameStateData(24, 90, true, 12345, 0.4f); // GCD < 0.5s
        var cantWeave = new GameStateData(24, 90, true, 12345, 1.0f); // GCD > 0.5s
        
        // Act & Assert
        canWeave.CanUseAbility().Should().BeTrue();
        cantWeave.CanUseAbility().Should().BeFalse();
    }
}
