// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.OnePassword.Tests
{
    public sealed class OnePasswordClientTests
    {
        [Fact]
        public async Task VersionCheckUsesConfiguredExecutableAndEnvironment()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, "2.34.0\n");
            var client = CreateClient(
                runner,
                new Dictionary<string, string?> { ["OP_ACCOUNT"] = "account-id" });

            var version = await client.GetVersionAsync();

            Assert.Equal("2.34.0", version);
            var invocation = Assert.Single(runner.Invocations);
            Assert.Equal("custom-op", invocation.ExecutablePath);
            Assert.Equal(new[] { "--version" }, invocation.Arguments);
            Assert.Equal("account-id", invocation.EnvironmentVariables["OP_ACCOUNT"]);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        public async Task AuthenticationProbeUsesWhoAmIAndReturnsExitStatus(
            int exitCode,
            bool expected)
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(exitCode, "output that must not affect the result");
            var client = CreateClient(
                runner,
                new Dictionary<string, string?> { ["OP_SESSION"] = "session-token" });

            var authenticated = await client.IsAuthenticatedAsync();

            Assert.Equal(expected, authenticated);
            var invocation = Assert.Single(runner.Invocations);
            Assert.Equal(new[] { "whoami" }, invocation.Arguments);
            Assert.Equal("session-token", invocation.EnvironmentVariables["OP_SESSION"]);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        public async Task SessionKeepAliveUsesAuthenticatedVaultAccessForConfiguredAccount(
            int exitCode,
            bool expected)
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(exitCode, "[]");
            var client = CreateClient(
                runner,
                new Dictionary<string, string?>
                {
                    ["OP_ACCOUNT"] = "account-id",
                    ["OP_SESSION_user-id"] = "session-token"
                });

            var succeeded = await client.KeepSessionAliveAsync();

            Assert.Equal(expected, succeeded);
            var invocation = Assert.Single(runner.Invocations);
            Assert.Equal(
                new[]
                {
                    "vault",
                    "list",
                    "--format",
                    "json",
                    "--account",
                    "account-id"
                },
                invocation.Arguments);
        }

        [Fact]
        public async Task DiagnosticHeartbeatRedactsSessionValuesAndBoundsErrorText()
        {
            const string sessionToken = "test-session-token-that-must-not-be-logged";
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(
                1,
                string.Empty,
                $"session {sessionToken} rejected\r\n{new string('x', 600)}");
            var client = CreateClient(
                runner,
                new Dictionary<string, string?>
                {
                    ["OP_ACCOUNT"] = "account-id",
                    ["OP_SESSION_user-id"] = sessionToken
                });

            var exception = await Assert.ThrowsAsync<OnePasswordHeartbeatException>(
                () => client.KeepSessionAliveWithDiagnosticsAsync());

            Assert.Contains("op vault list exited with code 1", exception.Message);
            Assert.Contains("[REDACTED]", exception.Message);
            Assert.DoesNotContain(sessionToken, exception.Message);
            Assert.DoesNotContain('\r', exception.Message);
            Assert.DoesNotContain('\n', exception.Message);
            Assert.EndsWith("...", exception.Message);
        }

        [Fact]
        public async Task AccountEnumerationReturnsStableUniqueIdentifiers()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(
                0,
                """
                [
                  { "account_uuid": "account-b", "user_uuid": "user-b" },
                  { "account_uuid": "account-a", "user_uuid": "user-a" },
                  { "account_uuid": "account-b", "user_uuid": "duplicate-user" }
                ]
                """);
            var client = CreateClient(runner);

            var accounts = await client.ListAccountsAsync();

            Assert.Collection(
                accounts,
                account =>
                {
                    Assert.Equal("account-a", account.AccountId);
                    Assert.Equal("user-a", account.UserId);
                },
                account =>
                {
                    Assert.Equal("account-b", account.AccountId);
                    Assert.Equal("user-b", account.UserId);
                });
            Assert.Equal(
                new[] { "account", "list", "--format", "json" },
                Assert.Single(runner.Invocations).Arguments);
        }

        [Fact]
        public async Task ItemEnumerationUsesSshKeyFilterAndStableIds()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(
                0,
                """
                [
                  {
                    "id": "item-b",
                    "title": "Beta",
                    "category": "SSH_KEY",
                    "vault": { "id": "vault-b" }
                  },
                  {
                    "id": "ignored",
                    "title": "Login",
                    "category": "LOGIN",
                    "vault": { "id": "vault-a" }
                  },
                  {
                    "id": "item-a",
                    "title": "Alpha",
                    "category": "SSH_KEY",
                    "vault": { "id": "vault-a" }
                  }
                ]
                """);
            var client = CreateClient(runner);

            var items = await client.ListSshKeyItemsAsync();

            Assert.Collection(
                items,
                item =>
                {
                    Assert.Equal("vault-a", item.VaultId);
                    Assert.Equal("item-a", item.ItemId);
                    Assert.Equal("Alpha", item.Title);
                },
                item =>
                {
                    Assert.Equal("vault-b", item.VaultId);
                    Assert.Equal("item-b", item.ItemId);
                    Assert.Equal("Beta", item.Title);
                });
            Assert.Equal(
                new[] { "item", "list", "--categories", "SSH Key", "--format", "json" },
                Assert.Single(runner.Invocations).Arguments);
        }

        [Theory]
        [InlineData("{\"id\":\"public_key\",\"label\":\"public key\",\"value\":\"ssh-ed25519 AAAA\"}")]
        [InlineData("[{\"id\":\"public_key\",\"label\":\"public key\",\"value\":\"ssh-ed25519 AAAA\"}]")]
        [InlineData("{\"fields\":[{\"id\":\"public_key\",\"value\":\"ssh-ed25519 AAAA\"}]}")]
        public async Task PublicKeyRetrievalAcceptsDocumentedJsonShapes(string json)
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, json);
            var client = CreateClient(runner);
            var item = new OnePasswordItem("vault-id", "item-id", "Test key");

            var publicKey = await client.GetPublicKeyAsync(item);

            Assert.Equal("ssh-ed25519 AAAA", publicKey);
            Assert.Equal(
                new[]
                {
                    "item",
                    "get",
                    "item-id",
                    "--vault",
                    "vault-id",
                    "--fields",
                    "label=public key",
                    "--format",
                    "json"
                },
                Assert.Single(runner.Invocations).Arguments);
        }

        [Fact]
        public async Task CommandFailureDoesNotExposeCapturedOutput()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(1, "secret stdout", "account details");
            var client = CreateClient(runner);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ListSshKeyItemsAsync());

            Assert.Contains("exit code 1", exception.Message);
            Assert.DoesNotContain("secret stdout", exception.Message);
            Assert.DoesNotContain("account details", exception.Message);
        }

        [Fact]
        public async Task PrivateKeyRetrievalUsesStableReferenceAndReturnsZeroableBytes()
        {
            var runner = new FakeOnePasswordCommandRunner();
            var secretBytes = "private key material"u8.ToArray();
            runner.EnqueueSecretResult(0, secretBytes);
            var client = CreateClient(runner);
            var item = new OnePasswordItem("vault-id", "item-id", "Test key");

            using (var privateKey = await client.GetPrivateKeyAsync(item))
            {
                Assert.Equal("private key material"u8.ToArray(), privateKey.Span.ToArray());
            }

            Assert.All(secretBytes, value => Assert.Equal(0, value));
            Assert.Equal(
                new[]
                {
                    "read",
                    "op://vault-id/item-id/private key?ssh-format=openssh",
                    "--no-newline"
                },
                Assert.Single(runner.Invocations).Arguments);
        }

        [Fact]
        public async Task FailedPrivateKeyRetrievalZeroesCapturedOutput()
        {
            var runner = new FakeOnePasswordCommandRunner();
            var capturedBytes = "partial secret"u8.ToArray();
            runner.EnqueueSecretResult(1, capturedBytes);
            var client = CreateClient(runner);
            var item = new OnePasswordItem("vault-id", "item-id", "Test key");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetPrivateKeyAsync(item));

            Assert.Contains("exit code 1", exception.Message);
            Assert.DoesNotContain("partial secret", exception.Message);
            Assert.All(capturedBytes, value => Assert.Equal(0, value));
        }

        [Fact]
        public async Task InvalidItemMetadataIsRejected()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(
                0,
                "[{\"id\":\"item-id\",\"title\":\"Key\",\"category\":\"SSH_KEY\",\"vault\":{}}]");
            var client = CreateClient(runner);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => client.ListSshKeyItemsAsync());
        }

        private static OnePasswordClient CreateClient(
            FakeOnePasswordCommandRunner runner,
            IReadOnlyDictionary<string, string?>? environmentVariables = null)
        {
            return new OnePasswordClient(
                new OnePasswordClientOptions
                {
                    ExecutablePath = "custom-op",
                    EnvironmentVariables = environmentVariables ??
                        new Dictionary<string, string?>()
                },
                runner);
        }
    }
}
