// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.Tests
{
    public sealed class SelfInstallationManagerTests
    {
        [Fact]
        public void InstallRefusesUnrelatedAliasWithoutForce()
        {
            using var fixture = new InstallationFixture();
            File.WriteAllText(
                Path.Combine(fixture.DestinationDirectory, "opssh"),
                "unrelated");

            var exception = Assert.Throws<InvalidOperationException>(
                () => SelfInstallationManager.Install(
                    fixture.SourcePath,
                    fixture.DestinationDirectory,
                    force: false));

            Assert.Contains("--force", exception.Message);
            Assert.Equal(
                "unrelated",
                File.ReadAllText(
                    Path.Combine(fixture.DestinationDirectory, "opssh")));
        }

        [Fact]
        public void UninstallRefusesUnrelatedAliasAndPreservesExecutable()
        {
            using var fixture = new InstallationFixture();
            var executablePath = Path.Combine(
                fixture.DestinationDirectory,
                "opexec");
            File.WriteAllText(executablePath, "installed-binary");
            File.WriteAllText(
                Path.Combine(fixture.DestinationDirectory, "opssh"),
                "unrelated");

            var exception = Assert.Throws<InvalidOperationException>(
                () => SelfInstallationManager.Uninstall(
                    fixture.SourcePath,
                    fixture.DestinationDirectory));

            Assert.Contains("unrelated path", exception.Message);
            Assert.Equal("installed-binary", File.ReadAllText(executablePath));
        }

        [Fact]
        public void InstallAndUninstallManageOneExecutableAndTwoAliases()
        {
            Assert.SkipUnless(
                OperatingSystem.IsLinux(),
                "Self-installation integration requires Linux filesystem semantics.");

            using var fixture = new InstallationFixture();

            SelfInstallationManager.Install(
                fixture.SourcePath,
                fixture.DestinationDirectory,
                force: false);

            var executablePath = Path.Combine(
                fixture.DestinationDirectory,
                "opexec");
            Assert.Equal("published-binary", File.ReadAllText(executablePath));
            Assert.Equal(
                "opexec",
                new FileInfo(
                    Path.Combine(fixture.DestinationDirectory, "opshell"))
                    .LinkTarget);
            Assert.Equal(
                "opexec",
                new FileInfo(
                    Path.Combine(fixture.DestinationDirectory, "opssh"))
                    .LinkTarget);

            SelfInstallationManager.Uninstall(
                fixture.SourcePath,
                fixture.DestinationDirectory);

            Assert.False(File.Exists(executablePath));
            Assert.False(File.Exists(
                Path.Combine(fixture.DestinationDirectory, "opshell")));
            Assert.False(File.Exists(
                Path.Combine(fixture.DestinationDirectory, "opssh")));
        }

        private sealed class InstallationFixture : IDisposable
        {
            private readonly string _rootDirectory;

            public InstallationFixture()
            {
                _rootDirectory = Path.Combine(
                    Path.GetTempPath(),
                    $"opexec-install-test-{Guid.NewGuid():N}");
                DestinationDirectory = Path.Combine(_rootDirectory, "bin");
                Directory.CreateDirectory(DestinationDirectory);
                SourcePath = Path.Combine(_rootDirectory, "source-opexec");
                File.WriteAllText(SourcePath, "published-binary");
            }

            public string SourcePath { get; }

            public string DestinationDirectory { get; }

            public void Dispose()
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
    }
}
