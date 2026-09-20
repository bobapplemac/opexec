// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    internal static class OpenSshPublicKey
    {
        public static bool TryParseEd25519(string value, out byte[] publicKeyBlob)
        {
            publicKeyBlob = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 2 ||
                !string.Equals(parts[0], SshAlgorithms.Ed25519, StringComparison.Ordinal))
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

            if (!reader.TryReadString(out var algorithm) ||
                !string.Equals(algorithm, SshAlgorithms.Ed25519, StringComparison.Ordinal) ||
                !reader.TryReadBlob(out var keyBytes) ||
                keyBytes.Length != 32 ||
                reader.RemainingLength != 0)
            {
                return false;
            }

            publicKeyBlob = decoded;
            return true;
        }
    }
}
