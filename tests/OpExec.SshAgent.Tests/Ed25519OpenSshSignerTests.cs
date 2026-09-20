// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class Ed25519OpenSshSignerTests
    {
        [Fact]
        public static void OpenSshSignatureVerifiesAgainstAdvertisedIdentity()
        {
            using var privateKey = OpenSshTestKey.CreatePrivateKeyBuffer();
            var identity = OpenSshTestKey.CreateIdentity();
            var data = "OpenSSH test payload"u8.ToArray();

            var signature = Ed25519OpenSshSigner.Sign(privateKey, identity, data);

            var keyReader = new AgentMessageReader(identity.PublicKeyBlob.Span);
            Assert.True(keyReader.TryReadString(out _));
            Assert.True(keyReader.TryReadBlob(out var publicKey));
            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKey.ToArray()));
            verifier.BlockUpdate(data, 0, data.Length);
            Assert.True(verifier.VerifySignature(signature.SignatureBlob.ToArray()));
        }

        [Theory]
        [InlineData("not a key")]
        [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----")]
        [InlineData("-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----")]
        public static void InvalidPrivateKeyIsRejectedWithoutLeakingInput(string value)
        {
            using var privateKey = CreateSecretBuffer(value);
            var identity = OpenSshTestKey.CreateIdentity();

            var exception = Assert.Throws<InvalidDataException>(
                () => Ed25519OpenSshSigner.Sign(
                    privateKey,
                    identity,
                    new byte[] { 1 }));

            Assert.DoesNotContain(value, exception.Message);
        }

        private static SecretBuffer CreateSecretBuffer(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            return new SecretBuffer(bytes, bytes.Length);
        }
    }
}
