// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class ShellResolver
    {
        private static readonly HashSet<string> LoginShells = new(
            new[] { "bash", "zsh", "sh", "dash", "ksh", "mksh", "fish" },
            StringComparer.Ordinal);

        public ShellResolution Resolve(bool loginShell)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Interactive shell execution is supported on Linux only.");
            }

            var shell = ResolvePath(
                Environment.GetEnvironmentVariable("SHELL"),
                Environment.UserName,
                ReadPasswdFile(),
                IsExecutable);
            var arguments = CreateArguments(shell, loginShell);
            return new ShellResolution(shell, arguments);
        }

        internal static string ResolvePath(
            string? environmentShell,
            string userName,
            string? passwdContent,
            Func<string, bool> isExecutable)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userName);
            ArgumentNullException.ThrowIfNull(isExecutable);

            if (!string.IsNullOrWhiteSpace(environmentShell) &&
                isExecutable(environmentShell))
            {
                return environmentShell;
            }

            var configuredShell = FindConfiguredShell(userName, passwdContent);

            if (!string.IsNullOrWhiteSpace(configuredShell) &&
                isExecutable(configuredShell))
            {
                return configuredShell;
            }

            if (isExecutable("/bin/sh"))
            {
                return "/bin/sh";
            }

            throw new FileNotFoundException(
                "Unable to find an executable shell through $SHELL, /etc/passwd, or /bin/sh.");
        }

        internal static IReadOnlyList<string> CreateArguments(
            string shellPath,
            bool loginShell)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(shellPath);

            if (!loginShell)
            {
                return new[] { "-i" };
            }

            var shellName = Path.GetFileName(shellPath);

            if (!LoginShells.Contains(shellName))
            {
                throw new NotSupportedException(
                    $"Login-shell behavior is not defined for '{shellName}'.");
            }

            return new[] { "-l" };
        }

        private static string? FindConfiguredShell(string userName, string? passwdContent)
        {
            if (string.IsNullOrWhiteSpace(passwdContent))
            {
                return null;
            }

            foreach (var line in passwdContent.Split('\n'))
            {
                var fields = line.TrimEnd('\r').Split(':');

                if (fields.Length >= 7 &&
                    string.Equals(fields[0], userName, StringComparison.Ordinal))
                {
                    return fields[6];
                }
            }

            return null;
        }

        private static string? ReadPasswdFile()
        {
            try
            {
                return File.ReadAllText("/etc/passwd");
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsExecutable(string path)
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var mode = File.GetUnixFileMode(path);
                const UnixFileMode executeBits =
                    UnixFileMode.UserExecute |
                    UnixFileMode.GroupExecute |
                    UnixFileMode.OtherExecute;
                return (mode & executeBits) != 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
        }
    }
}
