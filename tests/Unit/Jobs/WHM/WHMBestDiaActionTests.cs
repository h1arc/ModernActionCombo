using Xunit;
using FluentAssertions;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Tests.Unit.Jobs.WHM;

/// <summary>
/// Unit tests for WHM best DoT action resolution.
/// Tests the level-based progression: Aero (4+) → Aero II (46+) → Dia (72+)
/// </summary>
public class WHMBestDiaActionTests
{
    [Theory]
    [InlineData(1, 0u)]     // Below level 4 - no DoT
    [InlineData(3, 0u)]     // Below level 4 - no DoT
    [InlineData(4, 121u)]   // Level 4 - Aero
    [InlineData(20, 121u)]  // Mid-level - still Aero
    [InlineData(45, 121u)]  // Just before Aero II
    [InlineData(46, 132u)]  // Level 46 - Aero II
    [InlineData(60, 132u)]  // Mid-level - still Aero II
    [InlineData(71, 132u)]  // Just before Dia
    [InlineData(72, 16532u)] // Level 72 - Dia
    [InlineData(80, 16532u)] // High level - still Dia
    [InlineData(90, 16532u)] // Max level - still Dia
    public void ResolveActionForLevel_DoT_ReturnsCorrectActionForLevel(uint level, uint expectedAction)
    {
        // Arrange & Act - Test with any DoT action (they all resolve the same way)
        var result = WHMConstants.ResolveActionForLevel(121u, level); // Use Aero as base action
        
        // Assert
        result.Should().Be(expectedAction, $"Level {level} should return action {expectedAction}");
    }

    [Fact]
    public void GetDoTDebuff_ReturnsCorrectDebuffIds()
    {
        // Test each DoT action returns the correct debuff ID
        WHMConstants.GetDoTDebuff(16532u).Should().Be(1871u, "Dia should return Dia debuff ID");
        WHMConstants.GetDoTDebuff(132u).Should().Be(144u, "Aero II should return Aero II debuff ID");
        WHMConstants.GetDoTDebuff(121u).Should().Be(143u, "Aero should return Aero debuff ID");
        WHMConstants.GetDoTDebuff(0u).Should().Be(0u, "Unknown action should return 0");
    }

    [Theory]
    [InlineData(121u, 143u)]   // Aero → Aero debuff
    [InlineData(132u, 144u)]   // Aero II → Aero II debuff  
    [InlineData(16532u, 1871u)] // Dia → Dia debuff
    public void DoTAction_HasMatchingDebuffId(uint actionId, uint expectedDebuffId)
    {
        // This ensures the action-to-debuff mapping is consistent
        var debuffId = WHMConstants.GetDoTDebuff(actionId);
        debuffId.Should().Be(expectedDebuffId, $"Action {actionId} should map to debuff {expectedDebuffId}");
    }
}
