// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class OpenSshPublicKeyTests
    {
        private const string ValidPublicKey =
            "ssh-ed25519 " +
            "AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7 " +
            "test comment";

        [Fact]
        public void ValidEd25519PublicKeyIsParsed()
        {
            var result = OpenSshPublicKey.TryParseEd25519(
                ValidPublicKey,
                out var publicKeyBlob);

            Assert.True(result);
            Assert.Equal(
                "AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7",
                Convert.ToBase64String(publicKeyBlob));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ssh-rsa AAAA")]
        [InlineData("ssh-ed25519 not-base64")]
        [InlineData("ssh-ed25519 AAAA")]
        [InlineData("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7AA==")]
        public void InvalidOrUnsupportedPublicKeyIsRejected(string value)
        {
            Assert.False(OpenSshPublicKey.TryParseEd25519(value, out _));
        }
    }
}
