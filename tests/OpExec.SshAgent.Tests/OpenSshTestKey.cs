// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;

namespace OpExec.SshAgent.Tests
{
    // PUBLIC TEST FIXTURE: this private key is intentionally published and is not a secret.
    // Never authorize it on a real system or use it to protect data. See tests/README.md.
    internal static class OpenSshTestKey
    {
        public const string PublicKey =
            "ssh-ed25519 " +
            "AAAAC3NzaC1lZDI1NTE5AAAAIAlGsmc6JXJb2Ggc595dFN1ra7CxasGaayWVpB9Zf4Er";

        private const string PrivateKey =
            "-----BEGIN OPENSSH PRIVATE KEY-----\n" +
            "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n" +
            "QyNTUxOQAAACAJRrJnOiVyW9hoHOfeXRTda2uwsWrBmmsllaQfWX+BKwAAAKBdZcvPXWXL\n" +
            "zwAAAAtzc2gtZWQyNTUxOQAAACAJRrJnOiVyW9hoHOfeXRTda2uwsWrBmmsllaQfWX+BKw\n" +
            "AAAEDATcRX4AV0F7zZe2Ntsp33IHEyU83W6box/7+OrZ0xZQlGsmc6JXJb2Ggc595dFN1r\n" +
            "a7CxasGaayWVpB9Zf4ErAAAAG29wZXhlYy1taWxlc3RvbmUtNi10ZXN0LWtleQEC\n" +
            "-----END OPENSSH PRIVATE KEY-----\n";

        public static SecretBuffer CreatePrivateKeyBuffer()
        {
            var bytes = CreatePrivateKeyBytes();
            return new SecretBuffer(bytes, bytes.Length);
        }

        public static byte[] CreatePrivateKeyBytes()
        {
            return Encoding.ASCII.GetBytes(PrivateKey);
        }

        public static SshIdentity CreateIdentity()
        {
            AssertPublicKeyParses(out var publicKeyBlob);
            return new SshIdentity(
                SshAlgorithms.Ed25519,
                publicKeyBlob,
                "opexec-milestone-6-test-key");
        }

        private static void AssertPublicKeyParses(out byte[] publicKeyBlob)
        {
            if (!OpenSshPublicKey.TryParseEd25519(PublicKey, out publicKeyBlob))
            {
                throw new InvalidOperationException("The OpenSSH test key is invalid.");
            }
        }
    }
}
