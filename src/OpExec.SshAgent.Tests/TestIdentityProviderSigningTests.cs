// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class TestIdentityProviderSigningTests
    {
        [Fact]
        public async Task SignatureVerifiesAgainstAdvertisedPublicKey()
        {
            var provider = new TestIdentityProvider();
            var identity = Assert.Single(
                await provider.GetIdentitiesAsync(CancellationToken.None));
            var data = "controlled milestone 4 signing"u8.ToArray();

            var signature = await provider.SignAsync(
                identity,
                data,
                0,
                CancellationToken.None);

            Assert.Equal("ssh-ed25519", signature.Algorithm);
            Assert.Equal(64, signature.SignatureBlob.Length);

            var keyReader = new AgentMessageReader(identity.PublicKeyBlob.Span);
            Assert.True(keyReader.TryReadString(out var keyAlgorithm));
            Assert.Equal("ssh-ed25519", keyAlgorithm);
            Assert.True(keyReader.TryReadBlob(out var publicKeyBytes));
            Assert.Equal(0, keyReader.RemainingLength);

            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKeyBytes.ToArray()));
            verifier.BlockUpdate(data, 0, data.Length);
            Assert.True(verifier.VerifySignature(signature.SignatureBlob.ToArray()));
        }
    }
}
