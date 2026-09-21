// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Org.BouncyCastle.Pkcs;

namespace OpExec.SshAgent
{
    internal sealed class TestIdentityProvider : ISshIdentityProvider
    {
        public const string Algorithm = SshAlgorithms.Ed25519;
        public const string Comment = "opexec-milestone-4-test-key";

        // This deterministic development-only key is intentionally public knowledge and exists
        // only in the test assembly. It must never be used to protect real systems.
        private const string PrivateKeySeedBase64 =
            "rk0TtmQ63jFsNlj3cDpfQrgWJdKz40whgTBoebn/jgc=";

        private static readonly IReadOnlyList<SshIdentity> Identities =
            Array.AsReadOnly(
                new[]
                {
                    new SshIdentity(
                        Algorithm,
                        CreatePublicKeyBlob(),
                        Comment)
                });

        public Task<IReadOnlyList<SshIdentity>> GetIdentitiesAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Identities);
        }

        public Task<SshSignature> SignAsync(
            SshIdentity identity,
            ReadOnlyMemory<byte> data,
            uint flags,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!identity.MatchesPublicKeyBlob(Identities[0].PublicKeyBlob.Span))
            {
                return Task.FromException<SshSignature>(
                    new InvalidOperationException("The requested identity is not available."));
            }

            var seed = Convert.FromBase64String(PrivateKeySeedBase64);

            try
            {
                var privateKey = new Ed25519PrivateKeyParameters(seed);
                var signature = new byte[Ed25519PrivateKeyParameters.SignatureSize];
                privateKey.Sign(
                    Ed25519.Algorithm.Ed25519,
                    null,
                    data.Span,
                    signature);
                return Task.FromResult(new SshSignature(Algorithm, signature));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        }

        internal static byte[] CreatePrivateKeyPem(byte[]? privateKeySeed = null)
        {
            var seed = privateKeySeed?.ToArray() ??
                Convert.FromBase64String(PrivateKeySeedBase64);

            try
            {
                var privateKey = new Ed25519PrivateKeyParameters(seed);
                var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
                var base64 = Convert.ToBase64String(privateKeyInfo.GetEncoded());
                var lines = Enumerable.Range(0, (base64.Length + 63) / 64)
                    .Select(index => base64.Substring(
                        index * 64,
                        Math.Min(64, base64.Length - (index * 64))));
                var pem = "-----BEGIN PRIVATE KEY-----\n" +
                    string.Join("\n", lines) +
                    "\n-----END PRIVATE KEY-----\n";
                return Encoding.ASCII.GetBytes(pem);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        }

        private static byte[] CreatePublicKeyBlob()
        {
            var seed = Convert.FromBase64String(PrivateKeySeedBase64);

            try
            {
                var privateKey = new Ed25519PrivateKeyParameters(seed);
                var publicKey = privateKey.GeneratePublicKey().GetEncoded();
                var writer = new AgentMessageWriter();
                writer.WriteString(Algorithm);
                writer.WriteBlob(publicKey);
                return writer.ToArray();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        }
    }
}
