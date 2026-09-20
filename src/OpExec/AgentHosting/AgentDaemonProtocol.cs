// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;

namespace OpExec
{
    internal static class AgentDaemonProtocol
    {
        private const string ReadyPrefix = "READY\t";
        private const string ErrorPrefix = "ERROR\t";

        public static string CreateReady(string socketPath, string logPath)
        {
            return ReadyPrefix + Encode(socketPath) + "\t" + Encode(logPath);
        }

        public static string CreateError(string message)
        {
            return ErrorPrefix + Encode(message);
        }

        public static bool TryParseReady(
            string value,
            out string socketPath,
            out string logPath)
        {
            socketPath = string.Empty;
            logPath = string.Empty;

            if (!value.StartsWith(ReadyPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = value[ReadyPrefix.Length..].Split('\t');

            if (parts.Length != 2 ||
                !TryDecode(parts[0], out socketPath) ||
                !TryDecode(parts[1], out logPath))
            {
                socketPath = string.Empty;
                logPath = string.Empty;
                return false;
            }

            return true;
        }

        public static bool TryParseError(string value, out string message)
        {
            message = string.Empty;
            return value.StartsWith(ErrorPrefix, StringComparison.Ordinal) &&
                TryDecode(value[ErrorPrefix.Length..], out message);
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static bool TryDecode(string value, out string decoded)
        {
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                return true;
            }
            catch (FormatException)
            {
                decoded = string.Empty;
                return false;
            }
        }
    }
}
