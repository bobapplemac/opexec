// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace OpExec.SshAgent
{
    internal sealed class AgentMessageWriter
    {
        private readonly ArrayBufferWriter<byte> _buffer = new();

        public void WriteByte(byte value)
        {
            var destination = _buffer.GetSpan(1);
            destination[0] = value;
            _buffer.Advance(1);
        }

        public void WriteUInt32(uint value)
        {
            var destination = _buffer.GetSpan(sizeof(uint));
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
            _buffer.Advance(sizeof(uint));
        }

        public void WriteBlob(ReadOnlySpan<byte> value)
        {
            WriteUInt32(checked((uint)value.Length));
            var destination = _buffer.GetSpan(value.Length);
            value.CopyTo(destination);
            _buffer.Advance(value.Length);
        }

        public void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var maximumByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
            var destination = _buffer.GetSpan(sizeof(uint) + maximumByteCount);
            var byteCount = Encoding.UTF8.GetBytes(value, destination[sizeof(uint)..]);
            BinaryPrimitives.WriteUInt32BigEndian(destination, checked((uint)byteCount));
            _buffer.Advance(sizeof(uint) + byteCount);
        }

        public byte[] ToArray()
        {
            return _buffer.WrittenSpan.ToArray();
        }
    }
}
