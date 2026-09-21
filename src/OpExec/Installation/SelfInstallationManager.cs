// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        SelfInstallationManager.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Implements guarded system and per-user installation, hash-verified atomic copies,
//              relative alias creation, collision policy, permission assignment, and verified
//              uninstallation for the single self-contained executable.
// ------------------------------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OpExec
{
    internal sealed class SelfInstallationManager
    {
        private const string ExecutableName = "opexec";
        private static readonly string[] AliasNames = { "opshell", "opssh" };

        public int Run(InvocationRequest invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Self-installation is currently supported only on Linux.");
            }

            var destinationDirectory = ResolveDestinationDirectory(
                invocation.Action,
                invocation.UserInstallation);
            var sourcePath = ResolveSourcePath();

            if (invocation.Action == InvocationAction.Install)
            {
                Install(
                    sourcePath,
                    destinationDirectory,
                    invocation.Force);
                Console.Out.WriteLine(
                    $"Installed opexec, opshell, and opssh in {destinationDirectory}.");

                if (invocation.UserInstallation &&
                    !IsDirectoryOnPath(destinationDirectory))
                {
                    Console.Out.WriteLine(
                        $"Warning: {destinationDirectory} is not currently on PATH.");
                }
            }
            else
            {
                Uninstall(sourcePath, destinationDirectory);
                Console.Out.WriteLine(
                    $"Removed opexec, opshell, and opssh from {destinationDirectory}.");
            }

            return 0;
        }

        internal static void Install(
            string sourcePath,
            string destinationDirectory,
            bool force)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            sourcePath = Path.GetFullPath(sourcePath);
            destinationDirectory = Path.GetFullPath(destinationDirectory);
            Directory.CreateDirectory(destinationDirectory);
            var destinationPath = Path.Combine(
                destinationDirectory,
                ExecutableName);
            var aliases = AliasNames
                .Select(name => new AliasState(
                    Path.Combine(destinationDirectory, name),
                    destinationPath))
                .ToArray();

            ValidateInstallTargets(
                sourcePath,
                destinationPath,
                aliases,
                force);

            if (!PathsEqual(sourcePath, destinationPath))
            {
                AtomicCopyExecutable(sourcePath, destinationPath);
            }
            else
            {
                RestrictExecutableMode(destinationPath);
            }

            foreach (var alias in aliases)
            {
                AtomicCreateAlias(alias.Path);
            }
        }

        internal static void Uninstall(
            string sourcePath,
            string destinationDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            sourcePath = Path.GetFullPath(sourcePath);
            destinationDirectory = Path.GetFullPath(destinationDirectory);
            var destinationPath = Path.Combine(
                destinationDirectory,
                ExecutableName);
            var aliases = AliasNames
                .Select(name => new AliasState(
                    Path.Combine(destinationDirectory, name),
                    destinationPath))
                .ToArray();
            var destinationExists = PathEntryExists(destinationPath);

            foreach (var alias in aliases)
            {
                if (alias.Exists && !alias.IsOwned)
                {
                    throw new InvalidOperationException(
                        $"Refusing to remove unrelated path: {alias.Path}");
                }
            }

            if (destinationExists && Directory.Exists(destinationPath))
            {
                throw new InvalidOperationException(
                    $"Refusing to remove directory: {destinationPath}");
            }

            if (destinationExists &&
                !PathsEqual(sourcePath, destinationPath) &&
                !aliases.Any(alias => alias.IsOwned))
            {
                throw new InvalidOperationException(
                    $"The existing {destinationPath} cannot be verified as an opexec installation.");
            }

            foreach (var alias in aliases.Where(alias => alias.IsOwned))
            {
                File.Delete(alias.Path);
            }

            if (destinationExists)
            {
                File.Delete(destinationPath);
            }
        }

        internal static bool IsDirectoryOnPath(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            var expected = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar);
            var path = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            foreach (var value in path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                try
                {
                    var candidate = Path.GetFullPath(value)
                        .TrimEnd(Path.DirectorySeparatorChar);

                    if (string.Equals(
                            candidate,
                            expected,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is ArgumentException or NotSupportedException)
                {
                }
            }

            return false;
        }

        internal static string ResolveDestinationDirectory(
            InvocationAction action,
            bool userInstallation)
        {
            var effectiveUserId = geteuid();

            if (!userInstallation)
            {
                if (effectiveUserId != 0)
                {
                    var operation = action switch
                    {
                        InvocationAction.Uninstall => "Removing an installation from",
                        InvocationAction.Update => "Updating the installation in",
                        _ => "Installing to"
                    };
                    var option = action switch
                    {
                        InvocationAction.Uninstall => "--uninstall",
                        InvocationAction.Update => "--update",
                        _ => "--install"
                    };
                    throw new UnauthorizedAccessException(
                        $"{operation} /usr/local/bin requires root privileges.\n\n" +
                        "Retry for the system installation:\n" +
                        $"  sudo ./opexec {option}\n\n" +
                        "Or target only the current user's installation:\n" +
                        $"  ./opexec {option} --user");
                }

                return "/usr/local/bin";
            }

            if (effectiveUserId == 0 &&
                !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("SUDO_USER")))
            {
                throw new InvalidOperationException(
                    "The --user option must be run without sudo. " +
                    "Rerun the command as the intended user.");
            }

            var home = Environment.GetEnvironmentVariable("HOME");

            if (string.IsNullOrWhiteSpace(home))
            {
                throw new InvalidOperationException(
                    "The --user option requires HOME to identify the installation directory.");
            }

            return Path.GetFullPath(Path.Combine(home, ".local", "bin"));
        }

        private static string ResolveSourcePath()
        {
            var sourcePath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "The current opexec executable path could not be determined.");

            if (string.Equals(
                    Path.GetFileNameWithoutExtension(sourcePath),
                    "dotnet",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Self-installation requires a published self-contained opexec executable.");
            }

            return Path.GetFullPath(sourcePath);
        }

        private static void ValidateInstallTargets(
            string sourcePath,
            string destinationPath,
            IReadOnlyList<AliasState> aliases,
            bool force)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    "The running opexec executable could not be read.",
                    sourcePath);
            }

            if (Directory.Exists(destinationPath))
            {
                throw new InvalidOperationException(
                    $"Refusing to replace directory: {destinationPath}");
            }

            foreach (var alias in aliases)
            {
                if (Directory.Exists(alias.Path))
                {
                    throw new InvalidOperationException(
                        $"Refusing to replace directory: {alias.Path}");
                }

                if (alias.Exists && !alias.IsOwned && !force)
                {
                    throw new InvalidOperationException(
                        $"Refusing to replace unrelated path without --force: {alias.Path}");
                }
            }

            if (PathEntryExists(destinationPath) &&
                !PathsEqual(sourcePath, destinationPath) &&
                !aliases.All(alias => alias.IsOwned) &&
                !force)
            {
                throw new InvalidOperationException(
                    $"Refusing to replace an unverified {destinationPath} without --force.");
            }
        }

        private static void AtomicCopyExecutable(
            string sourcePath,
            string destinationPath)
        {
            var temporaryPath = CreateTemporaryPath(destinationPath);

            try
            {
                File.Copy(sourcePath, temporaryPath, overwrite: false);
                RestrictExecutableMode(temporaryPath);

                if (!HashesMatch(sourcePath, temporaryPath))
                {
                    throw new IOException(
                        "The copied opexec executable failed integrity verification.");
                }

                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void AtomicCreateAlias(string aliasPath)
        {
            var temporaryPath = CreateTemporaryPath(aliasPath);

            try
            {
                File.CreateSymbolicLink(temporaryPath, ExecutableName);
                File.Move(temporaryPath, aliasPath, overwrite: true);
            }
            finally
            {
                if (PathEntryExists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static bool HashesMatch(string firstPath, string secondPath)
        {
            using var first = File.OpenRead(firstPath);
            using var second = File.OpenRead(secondPath);
            var firstHash = SHA256.HashData(first);
            var secondHash = SHA256.HashData(second);

            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    firstHash,
                    secondHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(firstHash);
                CryptographicOperations.ZeroMemory(secondHash);
            }
        }

        private static void RestrictExecutableMode(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead |
                    UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead |
                    UnixFileMode.OtherExecute);
            }
        }

        private static bool PathEntryExists(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ||
                Directory.Exists(path) ||
                info.LinkTarget is not null;
        }

        private static string CreateTemporaryPath(string targetPath)
        {
            return Path.Combine(
                Path.GetDirectoryName(targetPath)!,
                $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        }

        private static bool PathsEqual(string first, string second)
        {
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.Ordinal);
        }

        [DllImport("libc")]
        private static extern uint geteuid();

        private sealed class AliasState
        {
            public AliasState(string path, string expectedTargetPath)
            {
                Path = path;
                var info = new FileInfo(path);
                LinkTarget = info.LinkTarget;
                Exists = info.Exists ||
                    Directory.Exists(path) ||
                    LinkTarget is not null;
                IsOwned = IsExpectedAlias(path, LinkTarget, expectedTargetPath);
            }

            public string Path { get; }

            public string? LinkTarget { get; }

            public bool Exists { get; }

            public bool IsOwned { get; }

            private static bool IsExpectedAlias(
                string aliasPath,
                string? linkTarget,
                string expectedTargetPath)
            {
                if (linkTarget is null)
                {
                    return false;
                }

                var resolvedTarget = System.IO.Path.IsPathRooted(linkTarget)
                    ? System.IO.Path.GetFullPath(linkTarget)
                    : System.IO.Path.GetFullPath(
                        System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(aliasPath)!,
                            linkTarget));
                return PathsEqual(resolvedTarget, expectedTargetPath);
            }
        }
    }
}
