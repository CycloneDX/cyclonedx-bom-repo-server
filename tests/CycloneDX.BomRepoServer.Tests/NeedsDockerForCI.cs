using System;
using System.Runtime.InteropServices;
using Xunit;

namespace CycloneDX.BomRepoServer.Tests
{
    public sealed class NeedsDockerForCITheory : TheoryAttribute
    {
        public NeedsDockerForCITheory()
        {
            if (IsNotLinux() && IsCI())
            {
                Skip = "Test skipped due to Docker (with Linux containers) is missing in runner";
            }
        }

        private static bool IsNotLinux() => !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static bool IsCI() => Environment.GetEnvironmentVariable("CI") == "true";
    }

    public sealed class NeedsDockerForCIFact : FactAttribute
    {
        public NeedsDockerForCIFact()
        {
            if (IsNotLinux() && IsCI())
            {
                Skip = "Test skipped due to Docker (with Linux containers) is missing in runner";
            }
        }

        private static bool IsNotLinux() => !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static bool IsCI() => Environment.GetEnvironmentVariable("CI") == "true";
    }
}