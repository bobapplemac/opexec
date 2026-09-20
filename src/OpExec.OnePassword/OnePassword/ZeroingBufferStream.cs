// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        ZeroingBufferStream.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Bounded mutable capture stream that clears discarded, resized, and disposed
//              buffers used for sensitive 1Password CLI output.
// ------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using OpExec.SshAgent;

namespace OpExec.OnePassword
{
    internal sealed class ZeroingBufferStream : Stream
    {
        private const int InitialCapacity = 4 * 1024;

        private readonly int _maximumLength;
        private byte[] _buffer;
        private int _length;
        private bool _detached;

        public ZeroingBufferStream(int maximumLength = int.MaxValue)
        {
            if (maximumLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            }

            _maximumLength = maximumLength;
            _buffer = new byte[Math.Min(InitialCapacity, maximumLength)];
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public SecretBuffer Detach()
        {
            ObjectDisposedException.ThrowIf(_detached, this);
            _detached = true;
            var result = new SecretBuffer(_buffer, _length);
            _buffer = Array.Empty<byte>();
            _length = 0;
            return result;
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_detached, this);
            EnsureCapacity(checked(_length + buffer.Length));
            buffer.CopyTo(_buffer.AsSpan(_length));
            _length += buffer.Length;
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_detached)
            {
                CryptographicOperations.ZeroMemory(_buffer);
                _buffer = Array.Empty<byte>();
                _length = 0;
                _detached = true;
            }

            base.Dispose(disposing);
        }

        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity > _maximumLength)
            {
                throw new InvalidDataException(
                    $"Secret command output exceeded the {_maximumLength}-byte safety limit.");
            }

            if (requiredCapacity <= _buffer.Length)
            {
                return;
            }

            var doubledCapacity = _buffer.Length > _maximumLength / 2
                ? _maximumLength
                : _buffer.Length * 2;
            var newCapacity = Math.Max(requiredCapacity, doubledCapacity);
            var newBuffer = new byte[newCapacity];
            _buffer.AsSpan(0, _length).CopyTo(newBuffer);
            CryptographicOperations.ZeroMemory(_buffer);
            _buffer = newBuffer;
        }
    }
}
