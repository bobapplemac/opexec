// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        AgentDaemonLog.cs
// Revision:    r13
// Modified:    2026-09-21
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Writes the detached SSH-agent's bounded, secret-safe diagnostics to a
//              lazily created state file with verified owner-only Unix permissions.
// ------------------------------------------------------------------------------------------

using System.Text;

namespace OpExec
{
    internal sealed class AgentDaemonLog : IDisposable
    {
        private const UnixFileMode PermissionMask =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute;

        private readonly object _sync = new();
        private StreamWriter? _writer;

        public AgentDaemonLog(bool verbose)
            : this(verbose, ResolvePath())
        {
        }

        internal AgentDaemonLog(bool verbose, string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            Verbose = verbose;
            Path = System.IO.Path.GetFullPath(path);

            if (verbose)
            {
                EnsureWriter();
            }
        }

        public bool Verbose { get; }

        public string Path { get; }

        public void WriteVerbose(string message)
        {
            if (Verbose)
            {
                Write(message);
            }
        }

        public void WriteFailure(string message)
        {
            Write(message);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private void Write(string message)
        {
            lock (_sync)
            {
                EnsureWriter();
                _writer!.WriteLine($"{DateTimeOffset.UtcNow:O} opssh: {message}");
                _writer.Flush();
            }
        }

        private void EnsureWriter()
        {
            if (_writer is not null)
            {
                return;
            }

            var directory = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(directory);

            if (!OperatingSystem.IsWindows())
            {
                const UnixFileMode directoryMode =
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute;
                SetAndVerifyUnixMode(
                    directory,
                    directoryMode,
                    "daemon diagnostic directory");
            }

            var stream = new FileStream(
                Path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    SetAndVerifyUnixMode(
                        Path,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        "daemon diagnostic log");
                }

                _writer = new StreamWriter(stream, new UTF8Encoding(false));
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static void SetAndVerifyUnixMode(
            string path,
            UnixFileMode expectedMode,
            string description)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Detached agent logs require Linux Unix file modes.");
            }

            File.SetUnixFileMode(path, expectedMode);
            var actualMode = File.GetUnixFileMode(path) & PermissionMask;

            if (actualMode != expectedMode)
            {
                throw new UnauthorizedAccessException(
                    $"The {description} permissions could not be restricted to " +
                    $"{Convert.ToString((int)expectedMode, 8)}.");
            }
        }

        private static string ResolvePath()
        {
            var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");

            if (string.IsNullOrWhiteSpace(stateHome))
            {
                var home = Environment.GetEnvironmentVariable("HOME");

                if (string.IsNullOrWhiteSpace(home))
                {
                    throw new InvalidOperationException(
                        "Daemon diagnostics require XDG_STATE_HOME or HOME.");
                }

                stateHome = System.IO.Path.Combine(home, ".local", "state");
            }

            return System.IO.Path.Combine(
                System.IO.Path.GetFullPath(stateHome),
                "opexec",
                $"opssh-agent-{Environment.ProcessId}.log");
        }
    }
}
