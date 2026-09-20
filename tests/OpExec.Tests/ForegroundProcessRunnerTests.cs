// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.Tests
{
    public sealed class ForegroundProcessRunnerTests
    {
        [Fact]
        public async Task ChildExitCodeIsReturnedUnchanged()
        {
            var request = CreateExitRequest(23);

            var exitCode = await new ForegroundProcessRunner().RunAsync(
                request,
                CancellationToken.None);

            Assert.Equal(23, exitCode);
        }

        [Fact]
        public async Task MissingExecutableProducesClearFailure()
        {
            var request = new ForegroundProcessRequest(
                "opexec-executable-that-does-not-exist",
                Array.Empty<string>(),
                Environment.CurrentDirectory);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new ForegroundProcessRunner().RunAsync(
                    request,
                    CancellationToken.None));

            Assert.Contains("Unable to start target executable", exception.Message);
        }

        [Fact]
        public async Task EnvironmentOverridesAreAddedToInheritedEnvironment()
        {
            var request = CreateEnvironmentCheckRequest();

            var exitCode = await new ForegroundProcessRunner().RunAsync(
                request,
                CancellationToken.None);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task CancellationStopsLongRunningChild()
        {
            var request = CreateLongRunningRequest();
            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(250));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new ForegroundProcessRunner().RunAsync(
                    request,
                    cancellation.Token));
        }

        [Fact]
        public void RequestEnvironmentIsCopiedAndCanBeOverlaidImmutably()
        {
            var source = new Dictionary<string, string?>
            {
                ["SSH_AUTH_SOCK"] = "/tmp/parent.sock"
            };
            var request = new ForegroundProcessRequest(
                "child",
                Array.Empty<string>(),
                Environment.CurrentDirectory,
                source);

            source["SSH_AUTH_SOCK"] = "/tmp/mutated.sock";
            var scoped = request.WithEnvironmentVariable(
                "SSH_AUTH_SOCK",
                "/tmp/integrated.sock");

            Assert.Equal("/tmp/parent.sock", request.EnvironmentVariables["SSH_AUTH_SOCK"]);
            Assert.Equal("/tmp/integrated.sock", scoped.EnvironmentVariables["SSH_AUTH_SOCK"]);
        }

        private static ForegroundProcessRequest CreateExitRequest(int exitCode)
        {
            if (OperatingSystem.IsWindows())
            {
                return new ForegroundProcessRequest(
                    "cmd.exe",
                    new[] { "/d", "/c", $"exit {exitCode}" },
                    Environment.CurrentDirectory);
            }

            return new ForegroundProcessRequest(
                "/bin/sh",
                new[] { "-c", $"exit {exitCode}" },
                Environment.CurrentDirectory);
        }

        private static ForegroundProcessRequest CreateEnvironmentCheckRequest()
        {
            var environment = new Dictionary<string, string?>
            {
                ["OPEXEC_TEST_VALUE"] = "expected"
            };

            if (OperatingSystem.IsWindows())
            {
                return new ForegroundProcessRequest(
                    "cmd.exe",
                    new[]
                    {
                        "/d",
                        "/c",
                        "if \"%OPEXEC_TEST_VALUE%\"==\"expected\" (exit 0) else (exit 19)"
                    },
                    Environment.CurrentDirectory,
                    environment);
            }

            return new ForegroundProcessRequest(
                "/bin/sh",
                new[] { "-c", "test \"$OPEXEC_TEST_VALUE\" = expected" },
                Environment.CurrentDirectory,
                environment);
        }

        private static ForegroundProcessRequest CreateLongRunningRequest()
        {
            if (OperatingSystem.IsWindows())
            {
                return new ForegroundProcessRequest(
                    "powershell.exe",
                    new[]
                    {
                        "-NoProfile",
                        "-NonInteractive",
                        "-Command",
                        "Start-Sleep -Seconds 30"
                    },
                    Environment.CurrentDirectory);
            }

            return new ForegroundProcessRequest(
                "/bin/sh",
                new[] { "-c", "sleep 30" },
                Environment.CurrentDirectory);
        }
    }
}
