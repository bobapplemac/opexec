// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        OpenSshPrivateKey.cs
// Revision:    r14
// Modified:    2026-09-21
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Decodes bounded OpenSSH private-key PEM data, parses it through Bouncy Castle,
//              and clears temporary buffers containing private key material.
// ------------------------------------------------------------------------------------------

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Utilities;

namespace OpExec.SshAgent
{
    internal static class OpenSshPrivateKey
    {
        private static ReadOnlySpan<byte> BeginMarker =>
            "-----BEGIN OPENSSH PRIVATE KEY-----"u8;

        private static ReadOnlySpan<byte> EndMarker =>
            "-----END OPENSSH PRIVATE KEY-----"u8;

        public static AsymmetricKeyParameter Parse(SecretBuffer privateKeyData)
        {
            ArgumentNullException.ThrowIfNull(privateKeyData);
            var privateKeyBlob = DecodePem(privateKeyData.Span);

            try
            {
                return OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(privateKeyBlob);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "The private key could not be parsed as an OpenSSH key.",
                    exception);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privateKeyBlob);
            }
        }

        private static byte[] DecodePem(ReadOnlySpan<byte> pem)
        {
            pem = TrimAsciiWhitespace(pem);

            if (!pem.StartsWith(BeginMarker) || !pem.EndsWith(EndMarker))
            {
                throw new InvalidDataException(
                    "The private key is not an OpenSSH private-key PEM value.");
            }

            var encoded = pem[BeginMarker.Length..^EndMarker.Length];
            var compact = new byte[encoded.Length];
            var compactLength = 0;

            try
            {
                foreach (var value in encoded)
                {
                    if (!IsAsciiWhitespace(value))
                    {
                        compact[compactLength++] = value;
                    }
                }

                if (compactLength == 0)
                {
                    throw new InvalidDataException(
                        "The OpenSSH private-key PEM value has no encoded data.");
                }

                var decoded = new byte[Base64.GetMaxDecodedFromUtf8Length(compactLength)];

                try
                {
                    var status = Base64.DecodeFromUtf8(
                        compact.AsSpan(0, compactLength),
                        decoded,
                        out var consumed,
                        out var written);

                    if (status != OperationStatus.Done || consumed != compactLength)
                    {
                        throw new InvalidDataException(
                            "The OpenSSH private-key PEM value contains invalid Base64 data.");
                    }

                    return decoded.AsSpan(0, written).ToArray();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(decoded);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(compact);
            }
        }

        private static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> value)
        {
            while (!value.IsEmpty && IsAsciiWhitespace(value[0]))
            {
                value = value[1..];
            }

            while (!value.IsEmpty && IsAsciiWhitespace(value[^1]))
            {
                value = value[..^1];
            }

            return value;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
        }
    }
}
