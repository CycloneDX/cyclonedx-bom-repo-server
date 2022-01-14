using System;
using Xunit;

namespace CycloneDX.BomRepoServer.Tests
{
    public sealed class NeedsDockerForCITheory : TheoryAttribute
    {
        public NeedsDockerForCITheory()
        {
            if (IsMacOsRunner() && IsCI())
            {
                Skip = "Test skipped due to Docker missing in macOS runner";
            }
        }

        private static bool IsMacOsRunner() => Environment.GetEnvironmentVariable("RUNNER_OS") == "macOS";

        private static bool IsCI() => Environment.GetEnvironmentVariable("CI") == "true";
    }

    public sealed class NeedsDockerForCIFact : FactAttribute
    {
        public NeedsDockerForCIFact()
        {
            if (IsMacOsRunner() && IsCI())
            {
                Skip = "Test skipped due to Docker missing in macOS runner";
            }
        }

        private static bool IsMacOsRunner() => Environment.GetEnvironmentVariable("RUNNER_OS") == "macOS";

        private static bool IsCI() => Environment.GetEnvironmentVariable("CI") == "true";
    }
}