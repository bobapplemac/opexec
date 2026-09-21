// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        RsaOpenSshSigner.cs
// Revision:    r14
// Modified:    2026-09-21
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Validates OpenSSH RSA key pairs and produces SHA-1 or SHA-2 PKCS#1 v1.5
//              signatures according to SSH-agent request flags.
// ------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace OpExec.SshAgent
{
    internal static class RsaOpenSshSigner
    {
        internal const uint Sha2_256Flag = 0x02;
        internal const uint Sha2_512Flag = 0x04;

        public static SshSignature Sign(
            SecretBuffer privateKeyData,
            SshIdentity identity,
            ReadOnlySpan<byte> data,
            uint flags)
        {
            ArgumentNullException.ThrowIfNull(privateKeyData);
            ArgumentNullException.ThrowIfNull(identity);

            if (OpenSshPrivateKey.Parse(privateKeyData) is not
                RsaPrivateCrtKeyParameters privateKey)
            {
                throw new InvalidDataException("The private key is not an RSA OpenSSH key.");
            }

            ValidatePublicKey(privateKey, identity);
            var (algorithm, digest) = SelectSignatureAlgorithm(flags);

            try
            {
                var signer = new RsaDigestSigner(digest);
                signer.Init(true, privateKey);
                signer.BlockUpdate(data);
                var signature = signer.GenerateSignature();
                var expectedLength = (privateKey.Modulus.BitLength + 7) / 8;

                if (signature.Length == expectedLength)
                {
                    return new SshSignature(algorithm, signature);
                }

                if (signature.Length > expectedLength)
                {
                    throw new InvalidDataException(
                        "The RSA signer returned an oversized signature.");
                }

                var paddedSignature = new byte[expectedLength];
                signature.CopyTo(paddedSignature, expectedLength - signature.Length);
                return new SshSignature(algorithm, paddedSignature);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("The RSA signing operation failed.", exception);
            }
        }

        private static (string Algorithm, IDigest Digest) SelectSignatureAlgorithm(uint flags)
        {
            if ((flags & Sha2_256Flag) != 0)
            {
                return (SshAlgorithms.RsaSha2_256, new Sha256Digest());
            }

            if ((flags & Sha2_512Flag) != 0)
            {
                return (SshAlgorithms.RsaSha2_512, new Sha512Digest());
            }

            return (SshAlgorithms.Rsa, new Sha1Digest());
        }

        private static void ValidatePublicKey(
            RsaPrivateCrtKeyParameters privateKey,
            SshIdentity identity)
        {
            if (!string.Equals(identity.Algorithm, SshAlgorithms.Rsa, StringComparison.Ordinal) ||
                !OpenSshPublicKey.TryReadRsaParameters(
                    identity.PublicKeyBlob.Span,
                    out var expectedExponent,
                    out var expectedModulus))
            {
                throw new InvalidDataException("The requested RSA public key is invalid.");
            }

            var actualExponent = privateKey.PublicExponent.ToByteArrayUnsigned();
            var actualModulus = privateKey.Modulus.ToByteArrayUnsigned();

            try
            {
                if (!CryptographicOperations.FixedTimeEquals(actualExponent, expectedExponent) ||
                    !CryptographicOperations.FixedTimeEquals(actualModulus, expectedModulus))
                {
                    throw new InvalidDataException(
                        "The private key returned by 1Password does not match the requested identity.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualExponent);
                CryptographicOperations.ZeroMemory(actualModulus);
            }
        }
    }
}
