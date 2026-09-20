// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Text;

namespace OpExec.SshAgent
{
    internal ref struct AgentMessageReader
    {
        private ReadOnlySpan<byte> _remaining;

        public AgentMessageReader(ReadOnlySpan<byte> message)
        {
            _remaining = message;
        }

        public int RemainingLength => _remaining.Length;

        public bool TryReadByte(out byte value)
        {
            if (_remaining.IsEmpty)
            {
                value = default;
                return false;
            }

            value = _remaining[0];
            _remaining = _remaining[1..];
            return true;
        }

        public bool TryReadUInt32(out uint value)
        {
            if (_remaining.Length < sizeof(uint))
            {
                value = default;
                return false;
            }

            value = BinaryPrimitives.ReadUInt32BigEndian(_remaining);
            _remaining = _remaining[sizeof(uint)..];
            return true;
        }

        public bool TryReadBlob(out ReadOnlySpan<byte> value)
        {
            value = default;

            if (_remaining.Length < sizeof(uint))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(_remaining);

            if (length > int.MaxValue || _remaining.Length - sizeof(uint) < (int)length)
            {
                return false;
            }

            value = _remaining.Slice(sizeof(uint), (int)length);
            _remaining = _remaining[(sizeof(uint) + (int)length)..];
            return true;
        }

        public bool TryReadString(out string value)
        {
            if (!TryReadBlob(out var bytes))
            {
                value = string.Empty;
                return false;
            }

            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
    }
}
