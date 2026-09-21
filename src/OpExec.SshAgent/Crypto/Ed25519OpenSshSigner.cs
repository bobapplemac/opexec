// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        Ed25519OpenSshSigner.cs
// Revision:    r14
// Modified:    2026-09-21
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Parses OpenSSH Ed25519 private-key material with Bouncy Castle, validates public
//              and private key agreement, signs SSH-agent payloads, and clears sensitive buffers.
// ------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpExec.SshAgent
{
    internal static class Ed25519OpenSshSigner
    {
        public static SshSignature Sign(
            SecretBuffer privateKeyData,
            SshIdentity identity,
            ReadOnlySpan<byte> data)
        {
            ArgumentNullException.ThrowIfNull(privateKeyData);
            ArgumentNullException.ThrowIfNull(identity);

            try
            {
                if (OpenSshPrivateKey.Parse(privateKeyData) is not
                    Ed25519PrivateKeyParameters privateKey)
                {
                    throw new InvalidDataException(
                        "The private key is not an Ed25519 OpenSSH key.");
                }

                ValidatePublicKey(privateKey, identity);
                var signature = new byte[Ed25519PrivateKeyParameters.SignatureSize];
                privateKey.Sign(
                    Org.BouncyCastle.Math.EC.Rfc8032.Ed25519.Algorithm.Ed25519,
                    null,
                    data,
                    signature);
                return new SshSignature(SshAlgorithms.Ed25519, signature);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "The private key could not be parsed as an OpenSSH Ed25519 key.",
                    exception);
            }
        }

        private static void ValidatePublicKey(
            Ed25519PrivateKeyParameters privateKey,
            SshIdentity identity)
        {
            var reader = new AgentMessageReader(identity.PublicKeyBlob.Span);

            if (!reader.TryReadString(out var algorithm) ||
                !string.Equals(algorithm, SshAlgorithms.Ed25519, StringComparison.Ordinal) ||
                !reader.TryReadBlob(out var expectedPublicKey) ||
                expectedPublicKey.Length != 32 ||
                reader.RemainingLength != 0)
            {
                throw new InvalidDataException("The requested Ed25519 public key is invalid.");
            }

            var actualPublicKey = privateKey.GeneratePublicKey().GetEncoded();

            try
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        actualPublicKey,
                        expectedPublicKey))
                {
                    throw new InvalidDataException(
                        "The private key returned by 1Password does not match the requested identity.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualPublicKey);
            }
        }

    }
}
