// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;

namespace OpExec.OnePassword.Tests
{
    // PUBLIC TEST FIXTURE: this private key is intentionally published and is not a secret.
    // Never authorize it on a real system or use it to protect data. See docs/testing.md.
    internal static class OpenSshTestKey
    {
        private const string PrivateKey =
            "-----BEGIN OPENSSH PRIVATE KEY-----\n" +
            "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n" +
            "QyNTUxOQAAACAJRrJnOiVyW9hoHOfeXRTda2uwsWrBmmsllaQfWX+BKwAAAKBdZcvPXWXL\n" +
            "zwAAAAtzc2gtZWQyNTUxOQAAACAJRrJnOiVyW9hoHOfeXRTda2uwsWrBmmsllaQfWX+BKw\n" +
            "AAAEDATcRX4AV0F7zZe2Ntsp33IHEyU83W6box/7+OrZ0xZQlGsmc6JXJb2Ggc595dFN1r\n" +
            "a7CxasGaayWVpB9Zf4ErAAAAG29wZXhlYy1taWxlc3RvbmUtNi10ZXN0LWtleQEC\n" +
            "-----END OPENSSH PRIVATE KEY-----\n";

        public static byte[] CreatePrivateKeyBytes()
        {
            return Encoding.ASCII.GetBytes(PrivateKey);
        }
    }
}
