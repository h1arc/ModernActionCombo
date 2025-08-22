using Xunit;
using FluentAssertions;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Tests.Unit.Jobs;

/// <summary>
/// Unit tests for the WHMConstants helper class.
/// Tests action resolution and constant definitions.
/// </summary>
public class WHMConstantsTests
{
    [Fact]
    public void WHMConstants_ActionIds_AreValid()
    {
        // Assert - All action IDs should be non-zero
        WHMConstants.Stone3.Should().BeGreaterThan(0);
        WHMConstants.Glare3.Should().BeGreaterThan(0);
        WHMConstants.Glare4.Should().BeGreaterThan(0);
        WHMConstants.Dia.Should().BeGreaterThan(0);
        WHMConstants.Holy.Should().BeGreaterThan(0);
        WHMConstants.PresenceOfMind.Should().BeGreaterThan(0);
        WHMConstants.AfflatusMisery.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void WHMConstants_JobId_IsWHM()
    {
        // Assert
        WHMConstants.WHMJobId.Should().Be(24, "WHM job ID should be 24");
    }
    
    [Fact]
    public void WHMConstants_BuffIds_AreValid()
    {
        // Assert
        WHMConstants.SacredSightBuffId.Should().BeGreaterThan(0);
        WHMConstants.DiaDebuffId.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void WHMConstants_ResolveAction_HandlesStoneVariants()
    {
        // Act & Assert - Test direct action resolution with level 90
        WHMConstants.ResolveActionForLevel(119, 90).Should().Be(25859u, "Stone should resolve to Glare III at level 90");
        WHMConstants.ResolveActionForLevel(127, 90).Should().Be(25859u, "Stone II should resolve to Glare III at level 90");
        WHMConstants.ResolveActionForLevel(3568, 90).Should().Be(25859u, "Stone III should resolve to Glare III at level 90");
        WHMConstants.ResolveActionForLevel(16533, 90).Should().Be(25859u, "Glare should resolve to Glare III at level 90");
        WHMConstants.ResolveActionForLevel(25859, 90).Should().Be(25859u, "Glare III should resolve to itself at level 90");
    }
    
    [Fact]
    public void WHMConstants_ResolveAction_HandlesAeroVariants()
    {
        // Act & Assert - Test direct action resolution with level 90
        WHMConstants.ResolveActionForLevel(121, 90).Should().Be(16532u, "Aero should resolve to Dia at level 90");
        WHMConstants.ResolveActionForLevel(132, 90).Should().Be(16532u, "Aero II should resolve to Dia at level 90");
        WHMConstants.ResolveActionForLevel(16532, 90).Should().Be(16532u, "Dia should resolve to itself at level 90");
    }
    
    [Fact]
    public void WHMConstants_ResolveAction_HandlesHolyVariants()
    {
        // Act & Assert - Test direct action resolution with level 90
        WHMConstants.ResolveActionForLevel(139, 90).Should().Be(25860u, "Holy should resolve to Holy III at level 90");
        WHMConstants.ResolveActionForLevel(25860, 90).Should().Be(25860u, "Holy III should resolve to itself at level 90");
    }
    
    [Fact]
    public void WHMConstants_ResolveAction_ReturnsOriginalForUnknown()
    {
        // Arrange
        uint unknownAction = 99999;
        
        // Act
        var result = WHMConstants.ResolveActionForLevel(unknownAction, 90);
        
        // Assert
        result.Should().Be(unknownAction, "should return original action for unknown actions");
    }
    
    [Fact]
    public void WHMConstants_GetDoTDebuff_ReturnsCorrectDebuff()
    {
        // Act & Assert
        WHMConstants.GetDoTDebuff(121).Should().Be(WHMConstants.AeroDebuffId, "Aero should return Aero debuff");
        // Aero II → Aero debuff
        WHMConstants.GetDoTDebuff(132).Should().Be(144u, "Aero II should return Aero II debuff");
        WHMConstants.GetDoTDebuff(16532).Should().Be(WHMConstants.DiaDebuffId, "Dia should return Dia debuff");
        WHMConstants.GetDoTDebuff(99999).Should().Be(0, "unknown action should return 0");
    }
}
