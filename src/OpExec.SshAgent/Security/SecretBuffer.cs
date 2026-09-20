// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        SecretBuffer.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Owns sensitive byte material in mutable storage and guarantees zeroing during
//              replacement and disposal.
// ------------------------------------------------------------------------------------------

using System.Security.Cryptography;

namespace OpExec.SshAgent
{
    public sealed class SecretBuffer : IDisposable
    {
        private byte[] _buffer;
        private readonly int _length;
        private bool _disposed;

        public SecretBuffer(byte[] buffer, int length)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (length < 0 || length > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            _buffer = buffer;
            _length = length;
        }

        public bool IsEmpty => _length == 0;

        public ReadOnlySpan<byte> Span
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _buffer.AsSpan(0, _length);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CryptographicOperations.ZeroMemory(_buffer);
            _buffer = Array.Empty<byte>();
        }
    }
}
