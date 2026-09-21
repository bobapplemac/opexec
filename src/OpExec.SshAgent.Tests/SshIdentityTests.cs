// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class SshIdentityTests
    {
        [Fact]
        public void IdentityClonesAndMatchesItsPublicKeyBlob()
        {
            var source = new byte[] { 1, 2, 3 };
            var identity = new SshIdentity("test", source, "comment");
            source[0] = 9;

            Assert.Equal(new byte[] { 1, 2, 3 }, identity.PublicKeyBlob.ToArray());
            Assert.True(identity.MatchesPublicKeyBlob(new byte[] { 1, 2, 3 }));
            Assert.False(identity.MatchesPublicKeyBlob(new byte[] { 9, 2, 3 }));
        }

        [Fact]
        public async Task TestProviderExposesExpectedOpenSshPublicKey()
        {
            var provider = new TestIdentityProvider();

            var identities = await provider.GetIdentitiesAsync(CancellationToken.None);

            var identity = Assert.Single(identities);
            var publicKeyLine = $"{identity.Algorithm} " +
                $"{Convert.ToBase64String(identity.PublicKeyBlob.Span)} {identity.Comment}";
            Assert.Equal(
                "ssh-ed25519 " +
                "AAAAC3NzaC1lZDI1NTE5AAAAIEJcViVfp8y1XUuQiPrrkOT0TVDrjCVgdCSu3/CaBio7 " +
                "opexec-milestone-4-test-key",
                publicKeyLine);
        }
    }
}
