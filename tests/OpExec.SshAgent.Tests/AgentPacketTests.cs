// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class AgentPacketTests
    {
        [Fact]
        public async Task PacketRoundTrips()
        {
            await using var stream = new MemoryStream();
            var payload = new byte[] { 1, 2, 3 };

            await AgentPacket.WriteAsync(stream, payload, CancellationToken.None);
            stream.Position = 0;
            var result = await AgentPacket.ReadAsync(stream, 100, CancellationToken.None);

            Assert.Equal(payload, result);
        }

        [Fact]
        public async Task CleanEndOfStreamReturnsNull()
        {
            await using var stream = new MemoryStream();

            var result = await AgentPacket.ReadAsync(stream, 100, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task TruncatedHeaderIsRejected()
        {
            await using var stream = new MemoryStream(new byte[] { 0, 0 });

            await Assert.ThrowsAsync<InvalidDataException>(
                () => AgentPacket.ReadAsync(stream, 100, CancellationToken.None));
        }

        [Fact]
        public async Task TruncatedPayloadIsRejected()
        {
            var packet = new byte[6];
            BinaryPrimitives.WriteUInt32BigEndian(packet, 3);
            packet[4] = 1;
            packet[5] = 2;
            await using var stream = new MemoryStream(packet);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => AgentPacket.ReadAsync(stream, 100, CancellationToken.None));
        }

        [Fact]
        public async Task OversizedPacketIsRejectedBeforePayloadAllocation()
        {
            var header = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(header, 101);
            await using var stream = new MemoryStream(header);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => AgentPacket.ReadAsync(stream, 100, CancellationToken.None));
        }
    }
}
