// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Reflection;
using OpExec.OnePassword;
using OpExec.SshAgent;
using Xunit;

namespace OpExec.Tests
{
    public sealed class ProductVersionTests
    {
        [Fact]
        public void VersionCommandUsesRevisionDisplayFormat()
        {
            var revisionLabel = ProductVersion.GetRevisionLabel(typeof(Program).Assembly);

            Assert.StartsWith("r15", revisionLabel, StringComparison.Ordinal);
            Assert.Equal($"opexec {revisionLabel}", CommandLineParser.GetVersion());
        }

        [Fact]
        public void ProductionAndTestAssembliesShareReleaseRevision()
        {
            AssertVersionMetadata(typeof(Program).Assembly, 15);
            AssertVersionMetadata(typeof(OnePasswordClient).Assembly, 15);
            AssertVersionMetadata(typeof(SshAgentOptions).Assembly, 15);
            AssertVersionMetadata(typeof(ProductVersionTests).Assembly, 15);
        }

        private static void AssertVersionMetadata(Assembly assembly, int revision)
        {
            Assert.Equal(new Version(1, 0, 0, 0), assembly.GetName().Version);
            Assert.Equal(
                $"1.{revision}.0.0",
                assembly
                    .GetCustomAttribute<AssemblyFileVersionAttribute>()
                    ?.Version);
            var canonicalRevision = $"r{revision}";
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? throw new InvalidOperationException(
                    $"{assembly.GetName().Name} has no informational version metadata.");

            Assert.True(
                string.Equals(
                    informationalVersion,
                    canonicalRevision,
                    StringComparison.Ordinal) ||
                informationalVersion.StartsWith(
                    $"{canonicalRevision}-",
                    StringComparison.Ordinal));
            Assert.Equal(informationalVersion, ProductVersion.GetRevisionLabel(assembly));
        }
    }
}
