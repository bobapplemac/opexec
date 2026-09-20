// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.Tests
{
    public sealed class CommandLineParserTests
    {
        private readonly CommandLineParser _parser = new();

        [Fact]
        public void OpExecStopsParsingAtChildCommand()
        {
            var result = _parser.Parse(
                "opexec",
                new[] { "--verbose", "ssh", "-V", "--help" });

            var request = AssertSuccess(result);
            Assert.Equal(InvocationMode.OpExec, request.Mode);
            Assert.Equal(InvocationAction.ExecuteCommand, request.Action);
            Assert.True(request.Verbose);
            Assert.Equal("ssh", request.Command);
            Assert.Equal(new[] { "-V", "--help" }, request.Arguments);
        }

        [Fact]
        public void OptionTerminatorAllowsDashPrefixedChildName()
        {
            var result = _parser.Parse("opexec", new[] { "--", "-child", "argument" });

            var request = AssertSuccess(result);
            Assert.Equal("-child", request.Command);
            Assert.Equal(new[] { "argument" }, request.Arguments);
        }

        [Fact]
        public void InteractiveModeRejectsChildCommand()
        {
            var result = _parser.Parse("opexec", new[] { "-i", "bash" });

            Assert.False(result.IsSuccess);
            Assert.Contains("cannot be combined", result.Error);
        }

        [Fact]
        public void BareOpExecShowsUsage()
        {
            var request = AssertSuccess(_parser.Parse("opexec", Array.Empty<string>()));

            Assert.Equal(InvocationAction.ShowUsage, request.Action);
        }

        [Fact]
        public void OpShellAliasSelectsNonLoginShell()
        {
            var request = AssertSuccess(_parser.Parse("/usr/local/bin/opshell", Array.Empty<string>()));

            Assert.Equal(InvocationMode.OpShell, request.Mode);
            Assert.Equal(InvocationAction.StartShell, request.Action);
            Assert.False(request.LoginShell);
        }

        [Theory]
        [InlineData("-l")]
        [InlineData("--login")]
        public void OpShellAliasSupportsLoginMode(string option)
        {
            var request = AssertSuccess(_parser.Parse("opshell", new[] { option }));

            Assert.Equal(InvocationAction.StartShell, request.Action);
            Assert.True(request.LoginShell);
        }

        [Fact]
        public void OpSshAliasPassesEveryArgumentThroughUnchanged()
        {
            var arguments = new[] { "-J", "bastion", "--help", "server" };

            var request = AssertSuccess(_parser.Parse("opssh", arguments));

            Assert.Equal(InvocationMode.OpSsh, request.Mode);
            Assert.Equal("ssh", request.Command);
            Assert.Equal(arguments, request.Arguments);
        }

        [Fact]
        public void OpSshAgentModeIsSelectedOnlyByFirstArgument()
        {
            var request = AssertSuccess(
                _parser.Parse("opssh", new[] { "--agent", "--daemon", "--verbose" }));

            Assert.Equal(InvocationAction.StartAgent, request.Action);
            Assert.True(request.Daemon);
            Assert.True(request.Verbose);
        }

        [Fact]
        public void OpSshAgentTokenAfterSshArgumentIsPassedThrough()
        {
            var arguments = new[] { "server", "--agent" };

            var request = AssertSuccess(_parser.Parse("opssh", arguments));

            Assert.Equal(InvocationAction.ExecuteCommand, request.Action);
            Assert.Equal(arguments, request.Arguments);
        }

        [Fact]
        public void OpSshAgentHelpUsesDedicatedAction()
        {
            var request = AssertSuccess(
                _parser.Parse("opssh", new[] { "--agent", "--help" }));

            Assert.Equal(InvocationAction.ShowAgentHelp, request.Action);
        }

        [Theory]
        [InlineData("opexec")]
        [InlineData("opshell")]
        public void CommonModesRecognizeLicensesOption(string invocationPath)
        {
            var request = AssertSuccess(
                _parser.Parse(invocationPath, new[] { "--licenses" }));

            Assert.Equal(InvocationAction.ShowLicenses, request.Action);
        }

        [Fact]
        public void OpSshRecognizesStandaloneLicensesOption()
        {
            var request = AssertSuccess(
                _parser.Parse("opssh", new[] { "--licenses" }));

            Assert.Equal(InvocationAction.ShowLicenses, request.Action);
        }

        [Fact]
        public void OpSshPreservesLicensesArgumentWhenItIsNotStandalone()
        {
            var arguments = new[] { "host", "--licenses" };
            var request = AssertSuccess(_parser.Parse("opssh", arguments));

            Assert.Equal(InvocationAction.ExecuteCommand, request.Action);
            Assert.Equal(arguments, request.Arguments);
        }

        [Fact]
        public void OpSshAgentRecognizesLicensesOption()
        {
            var request = AssertSuccess(
                _parser.Parse("opssh", new[] { "--agent", "--licenses" }));

            Assert.Equal(InvocationAction.ShowLicenses, request.Action);
        }

        [Fact]
        public void OpSshAgentRejectsUnknownOption()
        {
            var result = _parser.Parse("opssh", new[] { "--agent", "--unknown" });

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void OpSshAgentStopTargetsSelectedDaemon()
        {
            var request = AssertSuccess(
                _parser.Parse("opssh", new[] { "--agent", "--stop", "--verbose" }));

            Assert.Equal(InvocationAction.StopAgent, request.Action);
            Assert.True(request.Verbose);
            Assert.False(request.Daemon);
        }

        [Fact]
        public void OpSshAgentStopAllTargetsEveryDaemon()
        {
            var request = AssertSuccess(
                _parser.Parse("opssh", new[] { "--agent", "--stop-all" }));

            Assert.Equal(InvocationAction.StopAllAgents, request.Action);
        }

        [Theory]
        [InlineData("--stop", "--stop-all")]
        [InlineData("--stop", "--daemon")]
        [InlineData("--stop-all", "--daemon")]
        public void OpSshAgentRejectsConflictingLifecycleOptions(
            string first,
            string second)
        {
            var result = _parser.Parse(
                "opssh",
                new[] { "--agent", first, second });

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void InstallDefaultsToSystemScope()
        {
            var request = AssertSuccess(
                _parser.Parse("opexec", new[] { "--install" }));

            Assert.Equal(InvocationAction.Install, request.Action);
            Assert.False(request.UserInstallation);
            Assert.False(request.Force);
        }

        [Fact]
        public void UserInstallSupportsForce()
        {
            var request = AssertSuccess(
                _parser.Parse(
                    "opexec",
                    new[] { "--install", "--user", "--force" }));

            Assert.Equal(InvocationAction.Install, request.Action);
            Assert.True(request.UserInstallation);
            Assert.True(request.Force);
        }

        [Fact]
        public void UserUninstallIsRecognized()
        {
            var request = AssertSuccess(
                _parser.Parse("opexec", new[] { "--uninstall", "--user" }));

            Assert.Equal(InvocationAction.Uninstall, request.Action);
            Assert.True(request.UserInstallation);
        }

        [Theory]
        [InlineData("--install", "--uninstall")]
        [InlineData("--uninstall", "--force")]
        [InlineData("--user")]
        public void InvalidInstallationCombinationsAreRejected(params string[] arguments)
        {
            var result = _parser.Parse("opexec", arguments);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void InstallationCannotBeCombinedWithAChildCommand()
        {
            var result = _parser.Parse(
                "opexec",
                new[] { "--install", "command" });

            Assert.False(result.IsSuccess);
            Assert.Contains("cannot be combined", result.Error);
        }

        [Fact]
        public void WindowsExecutableSuffixDoesNotPreventAliasDispatch()
        {
            Assert.Equal(
                InvocationMode.OpSsh,
                CommandLineParser.ResolveMode("C:\\tools\\opssh.exe"));
        }

        [Fact]
        public void LinuxProcCommandLinePreservesInvokedAliasPath()
        {
            var commandLine =
                "/usr/local/bin/opssh\0server\0"u8.ToArray();

            var invocationPath = InvocationPathResolver.ParseFirstArgument(commandLine);

            Assert.Equal("/usr/local/bin/opssh", invocationPath);
            Assert.Equal(
                InvocationMode.OpSsh,
                CommandLineParser.ResolveMode(invocationPath!));
        }

        [Fact]
        public void VerboseAndQuietAreMutuallyExclusive()
        {
            var result = _parser.Parse("opexec", new[] { "-v", "-q", "command" });

            Assert.False(result.IsSuccess);
            Assert.Contains("cannot be used together", result.Error);
        }

        [Fact]
        public void UnknownWrapperOptionFailsBeforeChildCommand()
        {
            var result = _parser.Parse("opexec", new[] { "--unknown", "command" });

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
        }

        private static InvocationRequest AssertSuccess(CommandLineParseResult result)
        {
            Assert.True(result.IsSuccess, result.Error);
            return Assert.IsType<InvocationRequest>(result.Request);
        }
    }
}
