using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using ModernActionCombo.Jobs.WHM.Data;
using ModernActionCombo.Jobs.WHM;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Tests.Unit.Jobs.WHM;

/// <summary>
/// Unit tests for WHM Liturgy of the Bell functionality.
/// Tests the action replacement behavior when the Liturgy buff is active.
/// Note: These tests verify the SmartTargetInterceptor behavior without depending on GameStateCache.
/// </summary>
public class WHMLiturgyComboTests
{
    [Fact] 
    public void LiturgyCombo_SmartTargetSetup_IsProperlyConfigured()
    {
        // Arrange & Act
        var provider = new WHMProvider();
        var smartTargetRules = provider.GetSmartTargetRules();
        var liturgyRule = smartTargetRules.FirstOrDefault(r => r.ActionId == WHMConstants.LiturgyOfTheBell);

        // Assert
        liturgyRule.Should().NotBeNull("Liturgy smart target rule should exist");
        liturgyRule.ActionId.Should().Be(WHMConstants.LiturgyOfTheBell);
        liturgyRule.SecondaryActionId.Should().Be(WHMConstants.LiturgyOfTheBellBurst);
        liturgyRule.Mode.Should().Be(TargetingMode.GroundTargetSpecial);
    }

    [Fact]
    public void WHMConstants_LiturgyActions_AreProperlyDefined()
    {
        // Assert - Verify all Liturgy-related constants are defined
        WHMConstants.LiturgyOfTheBell.Should().Be(25862u, "Initial Liturgy action ID");
        WHMConstants.LiturgyOfTheBellBurst.Should().Be(28509u, "Follow-up Liturgy action ID");  
        WHMConstants.LiturgyOfTheBellBuffId.Should().Be(2709u, "Liturgy buff ID");
    }

    [Fact]
    public void SmartTargetResolver_LiturgyIsConfigured()
    {
        // Arrange
        var rules = new SmartTargetRule[]
        {
            new(WHMConstants.LiturgyOfTheBell,
                WHMConstants.LiturgyOfTheBellBurst,
                WHMConstants.LiturgyOfTheBellBuffId, 
                TargetingMode.GroundTargetSpecial)
        };

        // Act - Initialize resolver
        SmartTargetResolver.ClearForTesting();
        SmartTargetResolver.Initialize(rules);

        // Assert
        SmartTargetResolver.IsSmartTargetAction(WHMConstants.LiturgyOfTheBell)
            .Should().BeTrue("Original Liturgy should be a smart target action");
        SmartTargetResolver.IsSmartTargetAction(WHMConstants.LiturgyOfTheBellBurst)
            .Should().BeTrue("Liturgy burst should be a smart target action");
    }

    [Fact]
    public void LiturgyCombo_SmartTargetRuleMatches()
    {
        // Arrange
        var provider = new WHMProvider();
        var smartTargetRules = provider.GetSmartTargetRules();
        
        // Act - Check if Liturgy rule exists
        var hasLiturgyRule = smartTargetRules.Any(r => r.ActionId == WHMConstants.LiturgyOfTheBell);

        // Assert  
        hasLiturgyRule.Should().BeTrue("WHM provider should have a smart target rule for LiturgyOfTheBell");
    }

    [Fact]
    public void SmartTargetRules_CorrectTargetingMode()
    {
        // Test the GroundTargetSpecial targeting mode setup
        var rule = new SmartTargetRule(
            WHMConstants.LiturgyOfTheBell,
            WHMConstants.LiturgyOfTheBellBurst,
            WHMConstants.LiturgyOfTheBellBuffId,
            TargetingMode.GroundTargetSpecial);

        // Assert
        rule.ActionId.Should().Be(WHMConstants.LiturgyOfTheBell);
        rule.SecondaryActionId.Should().Be(WHMConstants.LiturgyOfTheBellBurst);
        rule.RequiredBuffId.Should().Be(WHMConstants.LiturgyOfTheBellBuffId);
        rule.Mode.Should().Be(TargetingMode.GroundTargetSpecial);
    }

    [Fact]
    public void LiturgyBurst_ShouldNotHaveSmartTargeting()
    {
        // Arrange - The key insight is that only LiturgyOfTheBell should be in smart targeting,
        // not LiturgyOfTheBellBurst since it's an instant AoE replacement
        var provider = new WHMProvider();
        var smartTargetRules = provider.GetSmartTargetRules();
        
        // Act - Find rules that target these actions as PRIMARY actions
        var liturgyRule = smartTargetRules.FirstOrDefault(r => r.ActionId == WHMConstants.LiturgyOfTheBell);
        var liturgyBurstAsMainAction = smartTargetRules.Where(r => r.ActionId == WHMConstants.LiturgyOfTheBellBurst).ToList();

        // Assert
        liturgyRule.Should().NotBeNull("Original Liturgy should have a smart target rule for ground placement");
        liturgyRule.SecondaryActionId.Should().Be(WHMConstants.LiturgyOfTheBellBurst, "Rule should specify Liturgy Burst as the replacement action");
        
        liturgyBurstAsMainAction.Should().BeEmpty("Liturgy Burst should NOT have its own smart target rule since it's instant AoE - it should only appear as SecondaryActionId");
        
        // Verify that Liturgy Burst appears as a secondary action, not primary
        var liturgyBurstAsSecondary = smartTargetRules.Where(r => r.SecondaryActionId == WHMConstants.LiturgyOfTheBellBurst).ToList();
        liturgyBurstAsSecondary.Should().HaveCount(1, "Liturgy Burst should appear exactly once as a secondary action");
    }
}