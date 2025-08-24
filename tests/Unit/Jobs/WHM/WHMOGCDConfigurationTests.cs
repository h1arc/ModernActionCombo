using System.Linq;
using Xunit;
using FluentAssertions;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Jobs.WHM;

namespace ModernActionCombo.Tests.Unit.Jobs.WHM;

/// <summary>
/// Unit tests for WHM oGCD configuration system.
/// Tests the individual rule enabling/disabling functionality.
/// </summary>
public class WHMOGCDConfigurationTests
{
    [Fact]
    public void GetNamedOGCDRules_ReturnsExpectedRules()
    {
        // Arrange
        var provider = new WHMProvider();
        
        // Act
        var rules = provider.GetNamedOGCDRules();
        
        // Assert
        rules.Should().NotBeNull();
        rules.Should().HaveCount(3);
        
        var ruleNames = rules.Select(r => r.Name).ToArray();
        ruleNames.Should().Contain("Lucid Dreaming");
        ruleNames.Should().Contain("Assize");
        ruleNames.Should().Contain("Presence of Mind");
    }
    
    [Fact]
    public void GetNamedComboRules_ReturnsExpectedStructure()
    {
        // Arrange
        var provider = new WHMProvider();
        
        // Act
        var rules = provider.GetNamedComboRules();
        
        // Assert
        rules.Should().NotBeNull();
        rules.Should().ContainKey("Single Target DPS");
        
        var stRules = rules["Single Target DPS"];
        stRules.Should().HaveCount(5); // 5 configurable rules (excluding default)
        
        // Verify each rule has a name and description
        foreach (var rule in stRules)
        {
            rule.Name.Should().NotBeNullOrWhiteSpace();
            rule.Rule.Should().NotBeNull();
            rule.Rule.Description.Should().NotBeNullOrWhiteSpace();
        }
    }
    
    [Fact]
    public void JobConfiguration_OGCDRulesDisabledByDefault()
    {
        // Arrange - Test that unknown rules are disabled by default (opt-in behavior)
        var config = new JobConfiguration();
        
        // Act & Assert - Rules that aren't explicitly enabled should be disabled
        config.IsOGCDRuleEnabled("Lucid Dreaming").Should().BeFalse();
        config.IsOGCDRuleEnabled("Assize").Should().BeFalse();
        config.IsOGCDRuleEnabled("Presence of Mind").Should().BeFalse();
    }
    
    [Fact]
    public void SetOGCDRuleEnabled_DisablesRule()
    {
        // Arrange
        var config = new JobConfiguration();
        config.SetOGCDRuleEnabled("Lucid Dreaming", true); // First enable it
        
        // Act
        config.SetOGCDRuleEnabled("Lucid Dreaming", false);
        
        // Assert
        config.IsOGCDRuleEnabled("Lucid Dreaming").Should().BeFalse();
        config.IsOGCDRuleEnabled("Assize").Should().BeFalse(); // Other rules still disabled by default
    }
    
    [Fact]
    public void SetOGCDRuleEnabled_EnablesRule()
    {
        // Arrange
        var config = new JobConfiguration();
        // Rules start disabled by default
        
        // Act
        config.SetOGCDRuleEnabled("Assize", true);
        
        // Assert
        config.IsOGCDRuleEnabled("Assize").Should().BeTrue();
        config.IsOGCDRuleEnabled("Lucid Dreaming").Should().BeFalse(); // Other rules still disabled
    }
}
