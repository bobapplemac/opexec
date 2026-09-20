// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;

namespace OpExec
{
    internal sealed class AgentDaemonLog : IDisposable
    {
        private readonly object _sync = new();
        private StreamWriter? _writer;

        public AgentDaemonLog(bool verbose)
        {
            Verbose = verbose;
            Path = ResolvePath();

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
                File.SetUnixFileMode(directory, directoryMode);
            }

            var stream = new FileStream(
                Path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    Path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            _writer = new StreamWriter(stream, new UTF8Encoding(false));
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
