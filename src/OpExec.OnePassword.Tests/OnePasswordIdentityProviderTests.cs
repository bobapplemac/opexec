// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.OnePassword.Tests
{
    public sealed class OnePasswordIdentityProviderTests
    {
        private const string PublicKey =
            "ssh-ed25519 " +
            "AAAAC3NzaC1lZDI1NTE5AAAAIAlGsmc6JXJb2Ggc595dFN1ra7CxasGaayWVpB9Zf4Er";

        private const string MismatchedPublicKey =
            "ssh-ed25519 " +
            "AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7";

        [Fact]
        public async Task DiscoveryCachesSupportedIdentitiesAndSkipsDuplicatesAndUnsupportedKeys()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, "2.34.0\n");
            runner.EnqueueResult(
                0,
                """
                [
                  {"id":"item-a","title":"Alpha","category":"SSH_KEY","vault":{"id":"vault-a"}},
                  {"id":"item-b","title":"Duplicate","category":"SSH_KEY","vault":{"id":"vault-a"}},
                  {"id":"item-c","title":"RSA","category":"SSH_KEY","vault":{"id":"vault-b"}},
                  {"id":"item-d","title":"Invalid","category":"SSH_KEY","vault":{"id":"vault-b"}}
                ]
                """);
            runner.EnqueueResult(0, PublicKeyJson(PublicKey));
            runner.EnqueueResult(0, PublicKeyJson(PublicKey));
            runner.EnqueueResult(0, PublicKeyJson(OpenSshTestKey.RsaPublicKey));
            runner.EnqueueResult(0, PublicKeyJson("ssh-rsa AAAA"));
            var client = new OnePasswordClient(new OnePasswordClientOptions(), runner);
            var logs = new List<string>();

            var provider = await OnePasswordIdentityProvider.CreateAsync(
                client,
                logs.Add,
                TestContext.Current.CancellationToken);
            var invocationCountAfterDiscovery = runner.Invocations.Count;
            var firstRead = await provider.GetIdentitiesAsync(CancellationToken.None);
            var secondRead = await provider.GetIdentitiesAsync(CancellationToken.None);

            Assert.Same(firstRead, secondRead);
            Assert.Collection(
                firstRead,
                identity =>
                {
                    Assert.Equal("Alpha", identity.Comment);
                    Assert.Equal("ssh-ed25519", identity.Algorithm);
                },
                identity =>
                {
                    Assert.Equal("RSA", identity.Comment);
                    Assert.Equal("ssh-rsa", identity.Algorithm);
                });
            Assert.Equal(invocationCountAfterDiscovery, runner.Invocations.Count);
            Assert.Contains("1Password SSH identities indexed: 2", logs);
            Assert.Contains(
                "1Password SSH identities skipped as unsupported or invalid: 1",
                logs);
            Assert.Contains("duplicate 1Password SSH identities skipped: 1", logs);
        }

        [Fact]
        public async Task EachSignRetrievesKeyAgainSignsAndZeroesSecretOutput()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, "2.34.0\n");
            runner.EnqueueResult(
                0,
                "[{\"id\":\"item-a\",\"title\":\"Alpha\",\"category\":\"SSH_KEY\",\"vault\":{\"id\":\"vault-a\"}}]");
            runner.EnqueueResult(0, PublicKeyJson(PublicKey));
            var firstPrivateKey = OpenSshTestKey.CreatePrivateKeyBytes();
            var secondPrivateKey = OpenSshTestKey.CreatePrivateKeyBytes();
            runner.EnqueueSecretResult(0, firstPrivateKey);
            runner.EnqueueSecretResult(0, secondPrivateKey);
            var logs = new List<string>();
            var provider = await OnePasswordIdentityProvider.CreateAsync(
                new OnePasswordClient(new OnePasswordClientOptions(), runner),
                logs.Add,
                TestContext.Current.CancellationToken);
            var identity = Assert.Single(
                await provider.GetIdentitiesAsync(CancellationToken.None));
            var data = "milestone 6 signing"u8.ToArray();

            var firstSignature = await provider.SignAsync(
                identity,
                data,
                0,
                CancellationToken.None);
            var secondSignature = await provider.SignAsync(
                identity,
                data,
                0,
                CancellationToken.None);

            Assert.Equal(firstSignature.SignatureBlob.ToArray(), secondSignature.SignatureBlob.ToArray());
            Assert.Equal(64, firstSignature.SignatureBlob.Length);
            Assert.All(firstPrivateKey, value => Assert.Equal(0, value));
            Assert.All(secondPrivateKey, value => Assert.Equal(0, value));
            Assert.Equal(
                2,
                runner.Invocations.Count(invocation =>
                    invocation.Arguments.Count > 0 && invocation.Arguments[0] == "read"));
            Assert.Equal(2, logs.Count(message => message.StartsWith("sign requested: SHA256:")));
            Assert.Equal(2, logs.Count(message => message.StartsWith("sign succeeded: SHA256:")));
        }

        [Theory]
        [InlineData(0U, "ssh-rsa")]
        [InlineData(0x02U, "rsa-sha2-256")]
        [InlineData(0x04U, "rsa-sha2-512")]
        public async Task RsaSigningHonorsAgentFlagsAndZeroesSecretOutput(
            uint flags,
            string expectedAlgorithm)
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, "2.34.0\n");
            runner.EnqueueResult(
                0,
                "[{\"id\":\"item-rsa\",\"title\":\"RSA\",\"category\":\"SSH_KEY\",\"vault\":{\"id\":\"vault-a\"}}]");
            runner.EnqueueResult(0, PublicKeyJson(OpenSshTestKey.RsaPublicKey));
            var privateKey = OpenSshTestKey.CreateRsaPrivateKeyBytes();
            runner.EnqueueSecretResult(0, privateKey);
            var provider = await OnePasswordIdentityProvider.CreateAsync(
                new OnePasswordClient(new OnePasswordClientOptions(), runner),
                cancellationToken: TestContext.Current.CancellationToken);
            var identity = Assert.Single(
                await provider.GetIdentitiesAsync(CancellationToken.None));

            var signature = await provider.SignAsync(
                identity,
                "1Password RSA signing"u8.ToArray(),
                flags,
                CancellationToken.None);

            Assert.Equal("ssh-rsa", identity.Algorithm);
            Assert.Equal(expectedAlgorithm, signature.Algorithm);
            Assert.Equal(256, signature.SignatureBlob.Length);
            Assert.All(privateKey, value => Assert.Equal(0, value));
        }

        [Fact]
        public async Task SigningRejectsPrivateKeyThatDoesNotMatchRequestedIdentity()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, "2.34.0\n");
            runner.EnqueueResult(
                0,
                "[{\"id\":\"item-a\",\"title\":\"Alpha\",\"category\":\"SSH_KEY\",\"vault\":{\"id\":\"vault-a\"}}]");
            runner.EnqueueResult(0, PublicKeyJson(MismatchedPublicKey));
            var mismatchedPrivateKey = OpenSshTestKey.CreatePrivateKeyBytes();
            runner.EnqueueSecretResult(0, mismatchedPrivateKey);
            var provider = await OnePasswordIdentityProvider.CreateAsync(
                new OnePasswordClient(new OnePasswordClientOptions(), runner),
                cancellationToken: TestContext.Current.CancellationToken);
            var identity = Assert.Single(
                await provider.GetIdentitiesAsync(CancellationToken.None));

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => provider.SignAsync(
                    identity,
                    new byte[] { 1 },
                    0,
                    CancellationToken.None));

            Assert.Contains("does not match", exception.Message);
            Assert.All(mismatchedPrivateKey, value => Assert.Equal(0, value));
        }

        [Fact]
        public async Task FailedPrivateKeyReadSignalsInvalidAuthentication()
        {
            var runner = CreateSigningRunner();
            runner.EnqueueSecretResult(1, Array.Empty<byte>());
            runner.EnqueueResult(1, string.Empty);
            var authenticationInvalidated = false;
            var provider = await OnePasswordIdentityProvider.CreateAsync(
                new OnePasswordClient(new OnePasswordClientOptions(), runner),
                cancellationToken: CancellationToken.None,
                authenticationInvalidated: () => authenticationInvalidated = true);
            var identity = Assert.Single(
                await provider.GetIdentitiesAsync(CancellationToken.None));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.SignAsync(
                    identity,
                    new byte[] { 1 },
                    0,
                    CancellationToken.None));

            Assert.True(authenticationInvalidated);
        }

        [Fact]
        public async Task FailedPrivateKeyReadDoesNotSignalValidAuthentication()
        {
            var runner = CreateSigningRunner();
            runner.EnqueueSecretResult(1, Array.Empty<byte>());
            runner.EnqueueResult(0, "authenticated");
            var authenticationInvalidated = false;
            var provider = await OnePasswordIdentityProvider.CreateAsync(
                new OnePasswordClient(new OnePasswordClientOptions(), runner),
                cancellationToken: CancellationToken.None,
                authenticationInvalidated: () => authenticationInvalidated = true);
            var identity = Assert.Single(
                await provider.GetIdentitiesAsync(CancellationToken.None));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.SignAsync(
                    identity,
                    new byte[] { 1 },
                    0,
                    CancellationToken.None));

            Assert.False(authenticationInvalidated);
        }

        private static FakeOnePasswordCommandRunner CreateSigningRunner()
        {
            var runner = new FakeOnePasswordCommandRunner();
            runner.EnqueueResult(0, "2.34.0\n");
            runner.EnqueueResult(
                0,
                "[{\"id\":\"item-a\",\"title\":\"Alpha\",\"category\":\"SSH_KEY\",\"vault\":{\"id\":\"vault-a\"}}]");
            runner.EnqueueResult(0, PublicKeyJson(PublicKey));
            return runner;
        }

        private static string PublicKeyJson(string publicKey)
        {
            return "{\"id\":\"public_key\",\"label\":\"public key\",\"value\":\"" +
                publicKey +
                "\"}";
        }
    }
}
