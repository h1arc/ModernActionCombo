using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ModernActionCombo.Tests
{
    public class ActionInterceptorNoSmartTargetReferencesTests
    {
        [Fact]
        public void ActionInterceptor_ShouldNotReference_SmartTargetResolver()
        {
            var root = FindRepoRoot();
            var path = Path.Combine(root, "src", "Core", "Action", "ActionInterceptor.cs");
            File.Exists(path).Should().BeTrue($"expected to find ActionInterceptor.cs at {path}");

            var text = File.ReadAllText(path);

            // Ensure no direct references to SmartTargetResolver appear in ActionInterceptor
            text.Should().NotContain("SmartTargetResolver", "ActionInterceptor must not depend on SmartTarget rules; SmartTargetInterceptor owns that logic");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            // Walk up until we find the solution file as an anchor
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ModernActionCombo.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
                throw new DirectoryNotFoundException("Could not locate repository root (ModernActionCombo.sln)");

            return dir.FullName;
        }
    }
}
