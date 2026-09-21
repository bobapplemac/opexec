// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    internal static class OpenSshPublicKey
    {
        internal const int MinimumRsaModulusBits = 1024;

        public static bool TryParse(
            string value,
            out string algorithm,
            out byte[] publicKeyBlob)
        {
            algorithm = string.Empty;
            publicKeyBlob = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 2)
            {
                return false;
            }

            byte[] decoded;

            try
            {
                decoded = Convert.FromBase64String(parts[1]);
            }
            catch (FormatException)
            {
                return false;
            }

            var reader = new AgentMessageReader(decoded);

            if (!reader.TryReadString(out var embeddedAlgorithm) ||
                !string.Equals(parts[0], embeddedAlgorithm, StringComparison.Ordinal))
            {
                return false;
            }

            var valid = embeddedAlgorithm switch
            {
                SshAlgorithms.Ed25519 =>
                    reader.TryReadBlob(out var keyBytes) &&
                    keyBytes.Length == 32 &&
                    reader.RemainingLength == 0,
                SshAlgorithms.Rsa =>
                    TryReadRsaParameters(
                        ref reader,
                        out _,
                        out var modulus) &&
                    GetBitLength(modulus) >= MinimumRsaModulusBits,
                _ => false
            };

            if (!valid)
            {
                return false;
            }

            algorithm = embeddedAlgorithm;
            publicKeyBlob = decoded;
            return true;
        }

        internal static bool TryReadRsaParameters(
            ReadOnlySpan<byte> publicKeyBlob,
            out byte[] exponent,
            out byte[] modulus)
        {
            exponent = Array.Empty<byte>();
            modulus = Array.Empty<byte>();
            var reader = new AgentMessageReader(publicKeyBlob);

            return reader.TryReadString(out var algorithm) &&
                string.Equals(algorithm, SshAlgorithms.Rsa, StringComparison.Ordinal) &&
                TryReadRsaParameters(ref reader, out exponent, out modulus);
        }

        private static bool TryReadRsaParameters(
            ref AgentMessageReader reader,
            out byte[] exponent,
            out byte[] modulus)
        {
            exponent = Array.Empty<byte>();
            modulus = Array.Empty<byte>();

            if (!reader.TryReadBlob(out var encodedExponent) ||
                !TryNormalizePositiveMpInt(encodedExponent, out exponent) ||
                !reader.TryReadBlob(out var encodedModulus) ||
                !TryNormalizePositiveMpInt(encodedModulus, out modulus) ||
                reader.RemainingLength != 0)
            {
                exponent = Array.Empty<byte>();
                modulus = Array.Empty<byte>();
                return false;
            }

            return true;
        }

        private static bool TryNormalizePositiveMpInt(
            ReadOnlySpan<byte> encoded,
            out byte[] normalized)
        {
            normalized = Array.Empty<byte>();

            if (encoded.IsEmpty || (encoded[0] & 0x80) != 0)
            {
                return false;
            }

            if (encoded[0] == 0)
            {
                if (encoded.Length == 1 || (encoded[1] & 0x80) == 0)
                {
                    return false;
                }

                encoded = encoded[1..];
            }

            normalized = encoded.ToArray();
            return true;
        }

        private static int GetBitLength(ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty)
            {
                return 0;
            }

            var significantBits = 8;
            var first = value[0];

            while ((first & 0x80) == 0)
            {
                significantBits--;
                first <<= 1;
            }

            return checked(((value.Length - 1) * 8) + significantBits);
        }
    }
}
