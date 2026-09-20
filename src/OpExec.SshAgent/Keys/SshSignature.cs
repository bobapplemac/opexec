// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    public sealed class SshSignature
    {
        private readonly byte[] _signatureBlob;

        public SshSignature(string algorithm, ReadOnlySpan<byte> signatureBlob)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

            if (signatureBlob.IsEmpty)
            {
                throw new ArgumentException("A signature blob is required.", nameof(signatureBlob));
            }

            Algorithm = algorithm;
            _signatureBlob = signatureBlob.ToArray();
        }

        public string Algorithm { get; }

        public ReadOnlyMemory<byte> SignatureBlob => _signatureBlob;
    }
}
