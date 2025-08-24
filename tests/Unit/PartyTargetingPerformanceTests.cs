using Xunit;
using ModernActionCombo.Tests;

namespace ModernActionCombo.Tests.Unit;

public class PartyTargetingPerformanceTests
{
    [Fact]
    public void RunPartyTargetingComparison()
    {
        // This will output the performance comparison to the test output
        PartyTargetingComparison.RunComparison();
    }
}
