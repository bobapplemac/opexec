// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.SshAgent;
using Xunit;

namespace OpExec.OnePassword.Tests
{
    public sealed class OnePasswordAuthenticationManagerTests
    {
        [Fact]
        public async Task ReusesInheritedAuthenticationWithoutSigningIn()
        {
            var probe = new FakeAuthenticationProbe(Array.Empty<OnePasswordAccount>(), true);
            var signIn = new FakeSignInRunner(
                new InteractiveSignInResult(0, "unused"));
            var statuses = new List<string>();
            var logs = new List<string>();

            using var authentication = await new OnePasswordAuthenticationManager(
                probe,
                signIn,
                statuses.Add,
                logs.Add).EstablishAsync(CancellationToken.None);

            Assert.Empty(authentication.EnvironmentVariables);
            Assert.Equal(1, probe.CallCount);
            Assert.Equal(0, signIn.CallCount);
            Assert.Empty(statuses);
            Assert.Equal(new[] { "authentication source: inherited" }, logs);
        }

        [Fact]
        public async Task CreatesAndValidatesScopedSessionWhenInheritedContextIsUnavailable()
        {
            var probe = new FakeAuthenticationProbe(
                new[] { new OnePasswordAccount("account-id", "user-id") },
                false,
                true);
            var signIn = new FakeSignInRunner(
                new InteractiveSignInResult(0, "session-token\r\n"));
            var statuses = new List<string>();
            var logs = new List<string>();

            var authentication = await new OnePasswordAuthenticationManager(
                probe,
                signIn,
                statuses.Add,
                logs.Add).EstablishAsync(CancellationToken.None);

            Assert.Equal("account-id", authentication.EnvironmentVariables["OP_ACCOUNT"]);
            Assert.Equal("session-token", authentication.EnvironmentVariables["OP_SESSION_user-id"]);
            Assert.Null(authentication.EnvironmentVariables["OP_SESSION"]);
            Assert.Equal(2, probe.CallCount);
            Assert.Equal("session-token", probe.Environments[1]["OP_SESSION_user-id"]);
            Assert.Equal(1, signIn.CallCount);
            Assert.Equal(new[] { "1Password authentication required." }, statuses);
            Assert.Equal(new[] { "authentication source: created" }, logs);

            authentication.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => authentication.EnvironmentVariables);
        }

        [Fact]
        public async Task ReportsInteractiveSignInFailureWithoutExposingOutput()
        {
            var manager = new OnePasswordAuthenticationManager(
                new FakeAuthenticationProbe(
                    new[] { new OnePasswordAccount("account-id", "user-id") },
                    false),
                new FakeSignInRunner(new InteractiveSignInResult(17, "secret")));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.EstablishAsync(CancellationToken.None));

            Assert.Contains("exit code 17", exception.Message);
            Assert.DoesNotContain("secret", exception.Message);
        }

        [Fact]
        public async Task RejectsSessionThatDoesNotAuthenticate()
        {
            var manager = new OnePasswordAuthenticationManager(
                new FakeAuthenticationProbe(
                    new[] { new OnePasswordAccount("account-id", "user-id") },
                    false,
                    false),
                new FakeSignInRunner(new InteractiveSignInResult(0, "session-token")));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.EstablishAsync(CancellationToken.None));

            Assert.Contains("did not accept", exception.Message);
        }

        [Fact]
        public async Task FindsSelectedAccountByValidatingAccountScopedVariables()
        {
            var probe = new FakeAuthenticationProbe(
                new[]
                {
                    new OnePasswordAccount("business-account", "business-user"),
                    new OnePasswordAccount("personal-account", "personal-user")
                },
                false,
                false,
                true);
            var manager = new OnePasswordAuthenticationManager(
                probe,
                new FakeSignInRunner(new InteractiveSignInResult(0, "session-token")));

            using var authentication = await manager.EstablishAsync(CancellationToken.None);

            Assert.Equal(
                "personal-account",
                authentication.EnvironmentVariables["OP_ACCOUNT"]);
            Assert.Equal(
                "session-token",
                authentication.EnvironmentVariables["OP_SESSION_personal-user"]);
            Assert.False(
                authentication.EnvironmentVariables.ContainsKey("OP_SESSION_business-user"));
        }

        [Fact]
        public async Task FailsClearlyWhenNoAccountsAreConfigured()
        {
            var signIn = new FakeSignInRunner(
                new InteractiveSignInResult(0, "unused"));
            var manager = new OnePasswordAuthenticationManager(
                new FakeAuthenticationProbe(Array.Empty<OnePasswordAccount>(), false),
                signIn);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.EstablishAsync(CancellationToken.None));

            Assert.Contains("op account add", exception.Message);
            Assert.Equal(0, signIn.CallCount);
        }

        [Fact]
        public async Task InteractiveSignInOutputCaptureAcceptsBoundedToken()
        {
            using var reader = new StringReader("session-token\n");

            var result = await InteractiveOnePasswordSignInRunner.ReadBoundedOutputAsync(
                reader,
                CancellationToken.None);

            Assert.Equal("session-token\n", result);
        }

        [Fact]
        public async Task InteractiveSignInOutputCaptureRejectsOversizedOutput()
        {
            var output = new string(
                'x',
                InteractiveOnePasswordSignInRunner.MaximumSessionOutputLength + 1);
            using var reader = new StringReader(output);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => InteractiveOnePasswordSignInRunner.ReadBoundedOutputAsync(
                    reader,
                    CancellationToken.None));

            Assert.Contains("unexpectedly large", exception.Message);
        }

        private sealed class FakeAuthenticationProbe : IOnePasswordAuthenticationProbe
        {
            private readonly Queue<bool> _results;
            private readonly IReadOnlyList<OnePasswordAccount> _accounts;

            public FakeAuthenticationProbe(
                IReadOnlyList<OnePasswordAccount> accounts,
                params bool[] results)
            {
                _accounts = accounts;
                _results = new Queue<bool>(results);
            }

            public int CallCount { get; private set; }

            public List<IReadOnlyDictionary<string, string?>> Environments { get; } = new();

            public Task<IReadOnlyList<OnePasswordAccount>> ListAccountsAsync(
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_accounts);
            }

            public Task<bool> IsAuthenticatedAsync(
                IReadOnlyDictionary<string, string?> environmentVariables,
                CancellationToken cancellationToken)
            {
                CallCount++;
                Environments.Add(new Dictionary<string, string?>(environmentVariables));
                return Task.FromResult(_results.Dequeue());
            }
        }

        private sealed class FakeSignInRunner : IInteractiveOnePasswordSignInRunner
        {
            private readonly InteractiveSignInResult _result;

            public FakeSignInRunner(InteractiveSignInResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<InteractiveSignInResult> RunAsync(
                CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }
    }
}
