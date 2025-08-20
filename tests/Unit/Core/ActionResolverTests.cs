using Xunit;
using FluentAssertions;
using ModernWrathCombo.Core.Services;

namespace ModernWrathCombo.Tests.Unit.Core;

/// <summary>
/// Unit tests for ActionResolver core functionality.
/// Tests dictionary-based action resolution performance and correctness.
/// </summary>
public class ActionResolverTests
{
    [Fact]
    public void Resolve_WithNoHandler_ReturnsOriginalAction()
    {
        // Arrange
        var resolver = new ActionResolver();
        const uint originalAction = 12345;

        // Act
        var result = resolver.Resolve(originalAction);

        // Assert
        result.Should().Be(originalAction);
    }

    [Fact]
    public void Resolve_WithRegisteredHandler_CallsHandler()
    {
        // Arrange
        var resolver = new ActionResolver();
        var mockHandler = new MockActionHandler(54321);
        const uint originalAction = 12345;
        
        resolver.RegisterHandler(originalAction, mockHandler);

        // Act
        var result = resolver.Resolve(originalAction);

        // Assert
        result.Should().Be(54321);
        mockHandler.WasCalled.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithGameState_CallsEnhancedHandler()
    {
        // Arrange
        var resolver = new ActionResolver();
        var mockHandler = new MockActionHandler(54321);
        const uint originalAction = 12345;
        
        resolver.RegisterHandler(originalAction, mockHandler);

        var gameState = new GameState(24, 90, true, 1001, 0.0f);
        var targetEffects = ReadOnlySpan<StatusEffect>.Empty;
        var playerEffects = ReadOnlySpan<StatusEffect>.Empty;
        var actionStates = ReadOnlySpan<ActionState>.Empty;

        // Act
        var result = resolver.Resolve(originalAction, gameState, targetEffects, playerEffects, actionStates);

        // Assert
        result.Should().Be(54321);
        mockHandler.WasCalledWithGameState.Should().BeTrue();
    }

    [Fact]
    public void RegisterHandler_MultipleActions_AllResolveSameHandler()
    {
        // Arrange
        var resolver = new ActionResolver();
        var mockHandler = new MockActionHandler(99999);
        ReadOnlySpan<uint> actionIds = stackalloc uint[] { 100, 200, 300 };
        
        resolver.RegisterHandler(actionIds, mockHandler);

        // Act & Assert
        foreach (var actionId in actionIds)
        {
            var result = resolver.Resolve(actionId);
            result.Should().Be(99999);
        }
        
        mockHandler.CallCount.Should().Be(3);
    }

    [Fact]
    public void ResolveBatch_ProcessesAllActions()
    {
        // Arrange
        var resolver = new ActionResolver();
        var mockHandler = new MockActionHandler(55555);
        
        resolver.RegisterHandler(100, mockHandler);
        
        ReadOnlySpan<uint> input = stackalloc uint[] { 100, 200, 100 };
        Span<uint> output = stackalloc uint[3];

        // Act
        resolver.ResolveBatch(input, output);

        // Assert
        output[0].Should().Be(55555); // Handled
        output[1].Should().Be(200);   // Not handled
        output[2].Should().Be(55555); // Handled
        mockHandler.CallCount.Should().Be(2);
    }

    [Fact]
    public void ClearHandlers_RemovesAllHandlers()
    {
        // Arrange
        var resolver = new ActionResolver();
        var mockHandler = new MockActionHandler(77777);
        
        resolver.RegisterHandler(123, mockHandler);
        resolver.HandlerCount.Should().Be(1);

        // Act
        resolver.ClearHandlers();

        // Assert
        resolver.HandlerCount.Should().Be(0);
        resolver.Resolve(123).Should().Be(123); // No longer handled
    }
}

/// <summary>
/// Mock action handler for testing purposes.
/// </summary>
internal class MockActionHandler : IActionHandler
{
    private readonly uint _returnValue;
    
    public bool WasCalled { get; private set; }
    public bool WasCalledWithGameState { get; private set; }
    public int CallCount { get; private set; }

    public MockActionHandler(uint returnValue)
    {
        _returnValue = returnValue;
    }

    public uint Execute(uint originalActionId)
    {
        WasCalled = true;
        CallCount++;
        return _returnValue;
    }

    public uint Execute(uint originalActionId, GameStateData gameState, ReadOnlySpan<StatusEffect> targetEffects, 
                      ReadOnlySpan<StatusEffect> playerEffects, ReadOnlySpan<ActionState> actionStates)
    {
        WasCalled = true;
        WasCalledWithGameState = true;
        CallCount++;
        return _returnValue;
    }
}
