// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class AgentMessageReaderWriterTests
    {
        [Fact]
        public void UInt32UsesBigEndianEncoding()
        {
            var writer = new AgentMessageWriter();
            writer.WriteUInt32(0x01020304);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, writer.ToArray());
        }

        [Fact]
        public void BlobRoundTrips()
        {
            var writer = new AgentMessageWriter();
            writer.WriteBlob(new byte[] { 10, 20, 30 });
            var reader = new AgentMessageReader(writer.ToArray());

            Assert.True(reader.TryReadBlob(out var value));
            Assert.Equal(new byte[] { 10, 20, 30 }, value.ToArray());
            Assert.Equal(0, reader.RemainingLength);
        }

        [Fact]
        public void TruncatedBlobIsRejected()
        {
            var reader = new AgentMessageReader(new byte[] { 0, 0, 0, 3, 1, 2 });

            Assert.False(reader.TryReadBlob(out _));
            Assert.Equal(6, reader.RemainingLength);
        }

        [Fact]
        public void Utf8StringRoundTrips()
        {
            var writer = new AgentMessageWriter();
            writer.WriteString("test-identity-\u2713");
            var reader = new AgentMessageReader(writer.ToArray());

            Assert.True(reader.TryReadString(out var value));
            Assert.Equal("test-identity-\u2713", value);
            Assert.Equal(0, reader.RemainingLength);
        }
    }
}
