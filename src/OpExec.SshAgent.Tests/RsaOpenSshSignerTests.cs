// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class RsaOpenSshSignerTests
    {
        [Theory]
        [InlineData(0U, "ssh-rsa")]
        [InlineData(RsaOpenSshSigner.Sha2_256Flag, "rsa-sha2-256")]
        [InlineData(RsaOpenSshSigner.Sha2_512Flag, "rsa-sha2-512")]
        [InlineData(
            RsaOpenSshSigner.Sha2_256Flag | RsaOpenSshSigner.Sha2_512Flag,
            "rsa-sha2-256")]
        [InlineData(0x80000000U, "ssh-rsa")]
        public void OpenSshSignatureUsesRequestedAlgorithmAndVerifies(
            uint flags,
            string expectedAlgorithm)
        {
            using var privateKey = RsaOpenSshTestKey.CreatePrivateKeyBuffer();
            var identity = RsaOpenSshTestKey.CreateIdentity();
            var data = "OpenSSH RSA test payload"u8.ToArray();

            var signature = RsaOpenSshSigner.Sign(privateKey, identity, data, flags);

            Assert.Equal(expectedAlgorithm, signature.Algorithm);
            Assert.Equal(256, signature.SignatureBlob.Length);
            Assert.True(Verify(identity, data, signature));
        }

        [Fact]
        public void MismatchedPrivateKeyIsRejected()
        {
            using var privateKey = RsaOpenSshTestKey.CreatePrivateKeyBuffer();
            var identity = RsaOpenSshTestKey.CreateIdentity();
            var mismatchedBlob = identity.PublicKeyBlob.ToArray();
            mismatchedBlob[^1] ^= 1;
            var mismatchedIdentity = new SshIdentity(
                identity.Algorithm,
                mismatchedBlob,
                identity.Comment);

            var exception = Assert.Throws<InvalidDataException>(
                () => RsaOpenSshSigner.Sign(
                    privateKey,
                    mismatchedIdentity,
                    new byte[] { 1 },
                    RsaOpenSshSigner.Sha2_512Flag));

            Assert.Contains("does not match", exception.Message);
        }

        [Fact]
        public void NonRsaPrivateKeyIsRejected()
        {
            using var privateKey = OpenSshTestKey.CreatePrivateKeyBuffer();
            var identity = RsaOpenSshTestKey.CreateIdentity();

            Assert.Throws<InvalidDataException>(
                () => RsaOpenSshSigner.Sign(
                    privateKey,
                    identity,
                    new byte[] { 1 },
                    RsaOpenSshSigner.Sha2_512Flag));
        }

        private static bool Verify(
            SshIdentity identity,
            byte[] data,
            SshSignature signature)
        {
            Assert.True(OpenSshPublicKey.TryReadRsaParameters(
                identity.PublicKeyBlob.Span,
                out var exponent,
                out var modulus));
            IDigest digest = signature.Algorithm switch
            {
                "ssh-rsa" => new Sha1Digest(),
                "rsa-sha2-256" => new Sha256Digest(),
                "rsa-sha2-512" => new Sha512Digest(),
                _ => throw new InvalidOperationException("Unexpected signature algorithm.")
            };
            var verifier = new RsaDigestSigner(digest);
            verifier.Init(
                false,
                new RsaKeyParameters(
                    false,
                    new BigInteger(1, modulus),
                    new BigInteger(1, exponent)));
            verifier.BlockUpdate(data);
            return verifier.VerifySignature(signature.SignatureBlob.ToArray());
        }
    }
}
