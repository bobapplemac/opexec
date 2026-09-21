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
            var result = OpenSshPublicKey.TryParse(
                ValidPublicKey,
                out var algorithm,
                out var publicKeyBlob);

            Assert.True(result);
            Assert.Equal("ssh-ed25519", algorithm);
            Assert.Equal(
                "AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7",
                Convert.ToBase64String(publicKeyBlob));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ssh-ed25519 not-base64")]
        [InlineData("ssh-ed25519 AAAA")]
        [InlineData("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7AA==")]
        public void InvalidOrUnsupportedPublicKeyIsRejected(string value)
        {
            Assert.False(OpenSshPublicKey.TryParse(value, out _, out _));
        }

        [Fact]
        public void ValidRsaPublicKeyIsParsed()
        {
            var result = OpenSshPublicKey.TryParse(
                RsaOpenSshTestKey.PublicKey,
                out var algorithm,
                out var publicKeyBlob);

            Assert.True(result);
            Assert.Equal("ssh-rsa", algorithm);
            Assert.Equal(
                RsaOpenSshTestKey.PublicKey.Split(' ')[1],
                Convert.ToBase64String(publicKeyBlob));
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 0, 1 })]
        [InlineData(new byte[] { 0x80 })]
        public void InvalidRsaExponentIsRejected(byte[] exponent)
        {
            var modulus = new byte[129];
            modulus[1] = 0x80;

            Assert.False(OpenSshPublicKey.TryParse(
                CreateRsaPublicKey(exponent, modulus),
                out _,
                out _));
        }

        [Fact]
        public void RsaModulusBelowOpenSshMinimumIsRejected()
        {
            var modulus = new byte[128];
            modulus[0] = 0x7f;

            Assert.False(OpenSshPublicKey.TryParse(
                CreateRsaPublicKey(new byte[] { 1, 0, 1 }, modulus),
                out _,
                out _));
        }

        private static string CreateRsaPublicKey(byte[] exponent, byte[] modulus)
        {
            var writer = new AgentMessageWriter();
            writer.WriteString("ssh-rsa");
            writer.WriteBlob(exponent);
            writer.WriteBlob(modulus);
            return $"ssh-rsa {Convert.ToBase64String(writer.ToArray())}";
        }
    }
}
