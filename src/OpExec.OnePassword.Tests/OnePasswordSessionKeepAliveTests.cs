// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Collections;
using Xunit;

namespace OpExec.OnePassword.Tests
{
    public sealed class OnePasswordSessionKeepAliveTests
    {
        private static readonly TimeSpan TestInterval = TimeSpan.FromMilliseconds(1);

        [Fact]
        public async Task ManualSessionRunsHeartbeatUntilDisposed()
        {
            var heartbeatCount = 0;
            var heartbeatObserved = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var heartbeatLogged = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var logs = new List<string>();
            await using var keepAlive = OnePasswordSessionKeepAlive.Start(
                CreateManualSessionEnvironment(),
                _ =>
                {
                    Interlocked.Increment(ref heartbeatCount);
                    heartbeatObserved.TrySetResult();
                    return Task.FromResult(true);
                },
                message =>
                {
                    logs.Add(message);

                    if (message == "1Password session keepalive succeeded")
                    {
                        heartbeatLogged.TrySetResult();
                    }
                },
                null,
                TestInterval,
                TestInterval,
                3,
                TestContext.Current.CancellationToken);

            await heartbeatObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            await heartbeatLogged.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            Assert.True(keepAlive.IsEnabled);
            Assert.True(Volatile.Read(ref heartbeatCount) >= 1);
            Assert.Contains("1Password session keepalive succeeded", logs);
        }

        [Fact]
        public async Task ConsecutiveFailuresInvalidateSessionAfterBoundedRetries()
        {
            var heartbeatCount = 0;
            var invalidated = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var logs = new List<string>();
            await using var keepAlive = OnePasswordSessionKeepAlive.Start(
                CreateManualSessionEnvironment(),
                _ =>
                {
                    Interlocked.Increment(ref heartbeatCount);
                    return Task.FromResult(false);
                },
                logs.Add,
                invalidated.SetResult,
                TestInterval,
                TestInterval,
                3,
                TestContext.Current.CancellationToken);

            await invalidated.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            Assert.Equal(3, Volatile.Read(ref heartbeatCount));
            Assert.Contains(
                "1Password session keepalive failed; retrying (1/3)",
                logs);
            Assert.Contains(
                "1Password session keepalive could not be renewed",
                logs);
        }

        [Fact]
        public async Task SuccessfulRetryResetsConsecutiveFailureCount()
        {
            var results = new Queue<bool>(new[] { false, true, false, false, false });
            var invalidated = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var logs = new List<string>();
            await using var keepAlive = OnePasswordSessionKeepAlive.Start(
                CreateManualSessionEnvironment(),
                _ => Task.FromResult(results.Dequeue()),
                logs.Add,
                invalidated.SetResult,
                TestInterval,
                TestInterval,
                3,
                TestContext.Current.CancellationToken);

            await invalidated.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            Assert.Empty(results);
            Assert.Contains("1Password session keepalive recovered", logs);
        }

        [Fact]
        public async Task NonSessionAuthenticationDoesNotStartHeartbeat()
        {
            var heartbeatCount = 0;
            await using var keepAlive = OnePasswordSessionKeepAlive.Start(
                CreateEnvironmentWithoutManualSessions(),
                _ =>
                {
                    Interlocked.Increment(ref heartbeatCount);
                    return Task.FromResult(true);
                },
                null,
                null,
                TestInterval,
                TestInterval,
                3,
                TestContext.Current.CancellationToken);

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                TestContext.Current.CancellationToken);

            Assert.False(keepAlive.IsEnabled);
            Assert.Equal(0, Volatile.Read(ref heartbeatCount));
        }

        private static IReadOnlyDictionary<string, string?> CreateManualSessionEnvironment()
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["OP_ACCOUNT"] = "account-id",
                ["OP_SESSION_user-id"] = "session-token"
            };
        }

        private static IReadOnlyDictionary<string, string?> CreateEnvironmentWithoutManualSessions()
        {
            var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                if (variable.Key is string name &&
                    (string.Equals(name, "OP_SESSION", StringComparison.Ordinal) ||
                     name.StartsWith("OP_SESSION_", StringComparison.Ordinal)))
                {
                    environment[name] = null;
                }
            }

            return environment;
        }
    }
}
