// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    public sealed class SshIdentity
    {
        private readonly byte[] _publicKeyBlob;

        public SshIdentity(string algorithm, ReadOnlySpan<byte> publicKeyBlob, string comment)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
            ArgumentNullException.ThrowIfNull(comment);

            if (publicKeyBlob.IsEmpty)
            {
                throw new ArgumentException("A public-key blob is required.", nameof(publicKeyBlob));
            }

            Algorithm = algorithm;
            _publicKeyBlob = publicKeyBlob.ToArray();
            Comment = comment;
        }

        public string Algorithm { get; }

        public ReadOnlyMemory<byte> PublicKeyBlob => _publicKeyBlob;

        public string Comment { get; }

        public bool MatchesPublicKeyBlob(ReadOnlySpan<byte> publicKeyBlob)
        {
            return publicKeyBlob.SequenceEqual(_publicKeyBlob);
        }
    }
}
