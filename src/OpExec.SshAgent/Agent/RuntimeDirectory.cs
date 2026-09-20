// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        RuntimeDirectory.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Creates and verifies private per-run Unix runtime directories and socket paths,
//              including path-length fallback, permissions, partial-startup recovery, and cleanup.
// ------------------------------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace OpExec.SshAgent
{
    internal sealed class RuntimeDirectory : IDisposable
    {
        private const int MaximumUnixSocketPathBytes = 107;
        private readonly Action<string>? _log;
        private bool _disposed;

        private RuntimeDirectory(string path, Action<string>? log)
        {
            Path = path;
            SocketPath = System.IO.Path.Combine(path, "agent.sock");
            _log = log;
        }

        public string Path { get; }

        public string SocketPath { get; }

        public static RuntimeDirectory Create(SshAgentOptions options)
        {
            var candidates = GetBaseDirectoryCandidates(options);

            foreach (var candidate in candidates)
            {
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    var randomName = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
                    var path = candidate.UseNameAsPrefix
                        ? System.IO.Path.Combine(candidate.BasePath, $"{options.RuntimeDirectoryName}-{candidate.UserId}-{randomName}")
                        : System.IO.Path.Combine(candidate.BasePath, options.RuntimeDirectoryName, randomName);
                    var socketPath = System.IO.Path.Combine(path, "agent.sock");

                    if (Encoding.UTF8.GetByteCount(socketPath) > MaximumUnixSocketPathBytes)
                    {
                        break;
                    }

                    if (Directory.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        CreatePrivateDirectory(path);
                        options.Log?.Invoke($"runtime directory created: {path}");
                        return new RuntimeDirectory(path, options.Log);
                    }
                    catch
                    {
                        TryDeletePartialDirectory(path);
                        throw;
                    }
                }
            }

            throw new PathTooLongException(
                $"Unable to create an SSH-agent socket path within the {MaximumUnixSocketPathBytes}-byte Unix limit.");
        }

        public static void RestrictSocketAccess(string socketPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                const UnixFileMode expectedMode =
                    UnixFileMode.UserRead | UnixFileMode.UserWrite;
                File.SetUnixFileMode(
                    socketPath,
                    expectedMode);
                VerifyUnixMode(socketPath, expectedMode, "SSH-agent socket");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (File.Exists(SocketPath))
                {
                    File.Delete(SocketPath);
                }

                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: false);
                }

                _log?.Invoke($"runtime directory removed: {Path}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log?.Invoke($"runtime directory cleanup failed: {exception.Message}");
            }
        }

        private static IReadOnlyList<BaseDirectoryCandidate> GetBaseDirectoryCandidates(
            SshAgentOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.RuntimeBaseDirectory))
            {
                return new[]
                {
                    new BaseDirectoryCandidate(options.RuntimeBaseDirectory, false, null)
                };
            }

            var candidates = new List<BaseDirectoryCandidate>();
            var xdgRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

            if (!string.IsNullOrWhiteSpace(xdgRuntimeDirectory))
            {
                candidates.Add(new BaseDirectoryCandidate(xdgRuntimeDirectory, false, null));
            }

            if (!OperatingSystem.IsWindows())
            {
                candidates.Add(new BaseDirectoryCandidate("/tmp", true, GetEffectiveUserId()));
            }
            else
            {
                candidates.Add(new BaseDirectoryCandidate(System.IO.Path.GetTempPath(), false, null));
            }

            return candidates;
        }

        private static void CreatePrivateDirectory(string path)
        {
            var parent = System.IO.Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("The runtime directory has no parent path.");
            Directory.CreateDirectory(parent);

            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
                return;
            }

            const UnixFileMode expectedMode =
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(path, expectedMode);
            File.SetUnixFileMode(path, expectedMode);
            VerifyUnixMode(path, expectedMode, "SSH-agent runtime directory");
        }

        [UnsupportedOSPlatform("windows")]
        private static void VerifyUnixMode(
            string path,
            UnixFileMode expectedMode,
            string description)
        {
            const UnixFileMode permissionBits =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute;
            var actualMode = File.GetUnixFileMode(path) & permissionBits;

            if (actualMode != expectedMode)
            {
                throw new UnauthorizedAccessException(
                    $"The {description} permissions are {Convert.ToString((int)actualMode, 8)}, " +
                    $"but {Convert.ToString((int)expectedMode, 8)} is required.");
            }
        }

        private static uint GetEffectiveUserId()
        {
            return geteuid();
        }

        private static void TryDeletePartialDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: false);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        [DllImport("libc")]
        private static extern uint geteuid();

        private sealed class BaseDirectoryCandidate
        {
            public BaseDirectoryCandidate(
                string basePath,
                bool useNameAsPrefix,
                uint? userId)
            {
                BasePath = System.IO.Path.GetFullPath(basePath);
                UseNameAsPrefix = useNameAsPrefix;
                UserId = userId;
            }

            public string BasePath { get; }

            public bool UseNameAsPrefix { get; }

            public uint? UserId { get; }
        }
    }
}
