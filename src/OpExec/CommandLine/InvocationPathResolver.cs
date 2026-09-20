// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;

namespace OpExec
{
    internal static class InvocationPathResolver
    {
        public static string Resolve()
        {
            if (OperatingSystem.IsLinux())
            {
                try
                {
                    var invocationPath = ReadLinuxInvocationPath();

                    if (!string.IsNullOrWhiteSpace(invocationPath))
                    {
                        return invocationPath;
                    }
                }
                catch (IOException)
                {
                    // Fall back to the runtime process path below.
                }
                catch (UnauthorizedAccessException)
                {
                    // Fall back to the runtime process path below.
                }
            }

            return Environment.ProcessPath
                ?? Environment.GetCommandLineArgs().FirstOrDefault()
                ?? "opexec";
        }

        internal static string? ParseFirstArgument(ReadOnlySpan<byte> commandLine)
        {
            var terminator = commandLine.IndexOf((byte)0);
            var firstArgument = terminator >= 0
                ? commandLine[..terminator]
                : commandLine;

            return firstArgument.IsEmpty
                ? null
                : Encoding.UTF8.GetString(firstArgument);
        }

        private static string? ReadLinuxInvocationPath()
        {
            const int maximumPathBytes = 32768;
            using var commandLine = File.OpenRead("/proc/self/cmdline");
            var buffer = new byte[256];
            var length = 0;

            while (length < maximumPathBytes)
            {
                var value = commandLine.ReadByte();

                if (value <= 0)
                {
                    break;
                }

                if (length == buffer.Length)
                {
                    Array.Resize(
                        ref buffer,
                        Math.Min(buffer.Length * 2, maximumPathBytes));
                }

                buffer[length++] = (byte)value;
            }

            return length == 0
                ? null
                : Encoding.UTF8.GetString(buffer, 0, length);
        }
    }
}
