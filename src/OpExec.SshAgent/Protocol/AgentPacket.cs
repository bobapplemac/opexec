// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;

namespace OpExec.SshAgent
{
    internal static class AgentPacket
    {
        public static async Task<byte[]?> ReadAsync(
            Stream stream,
            int maximumPacketLength,
            CancellationToken cancellationToken)
        {
            if (maximumPacketLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPacketLength));
            }

            var header = new byte[sizeof(uint)];
            var headerBytesRead = await ReadUpToAsync(stream, header, cancellationToken);

            if (headerBytesRead == 0)
            {
                return null;
            }

            if (headerBytesRead != header.Length)
            {
                throw new InvalidDataException("The SSH-agent packet header is truncated.");
            }

            var packetLength = BinaryPrimitives.ReadUInt32BigEndian(header);

            if (packetLength == 0)
            {
                throw new InvalidDataException("The SSH-agent packet payload cannot be empty.");
            }

            if (packetLength > maximumPacketLength)
            {
                throw new InvalidDataException(
                    $"The SSH-agent packet length {packetLength} exceeds the configured maximum of {maximumPacketLength} bytes.");
            }

            var payload = new byte[packetLength];
            var payloadBytesRead = await ReadUpToAsync(stream, payload, cancellationToken);

            if (payloadBytesRead != payload.Length)
            {
                throw new InvalidDataException("The SSH-agent packet payload is truncated.");
            }

            return payload;
        }

        public static async Task WriteAsync(
            Stream stream,
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken)
        {
            if (payload.IsEmpty)
            {
                throw new ArgumentException("The SSH-agent packet payload cannot be empty.", nameof(payload));
            }

            var header = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)payload.Length));
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task<int> ReadUpToAsync(
            Stream stream,
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            var totalBytesRead = 0;

            while (totalBytesRead < destination.Length)
            {
                var bytesRead = await stream.ReadAsync(
                    destination[totalBytesRead..],
                    cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }
}
