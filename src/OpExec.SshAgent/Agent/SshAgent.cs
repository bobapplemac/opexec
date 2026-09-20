// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        SshAgent.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Public lifetime wrapper that composes the isolated runtime directory and SSH-agent
//              socket server into a single asynchronously disposable agent instance.
// ------------------------------------------------------------------------------------------

namespace OpExec.SshAgent
{
    public sealed class SshAgent : IAsyncDisposable
    {
        private readonly AgentServer _server;
        private readonly RuntimeDirectory _runtimeDirectory;
        private bool _disposed;

        private SshAgent(AgentServer server, RuntimeDirectory runtimeDirectory)
        {
            _server = server;
            _runtimeDirectory = runtimeDirectory;
        }

        public string SocketPath => _runtimeDirectory.SocketPath;

        public static async Task<SshAgent> StartAsync(
            SshAgentOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ValidateOptions(options);
            cancellationToken.ThrowIfCancellationRequested();

            RuntimeDirectory? runtimeDirectory = null;
            AgentServer? server = null;

            try
            {
                runtimeDirectory = RuntimeDirectory.Create(options);
                server = new AgentServer(
                    runtimeDirectory.SocketPath,
                    options.MaximumPacketLength,
                    options.IdentityProvider!,
                    options.Log);
                server.Start(cancellationToken);

                return new SshAgent(server, runtimeDirectory);
            }
            catch
            {
                try
                {
                    if (server is not null)
                    {
                        await server.DisposeAsync();
                    }
                }
                catch
                {
                    // Preserve the original startup failure.
                }
                finally
                {
                    runtimeDirectory?.Dispose();
                }

                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                await _server.DisposeAsync();
            }
            finally
            {
                _runtimeDirectory.Dispose();
            }
        }

        private static void ValidateOptions(SshAgentOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.RuntimeDirectoryName))
            {
                throw new ArgumentException("A runtime directory name is required.", nameof(options));
            }

            if (options.RuntimeDirectoryName is "." or "..")
            {
                throw new ArgumentException(
                    "The runtime directory name cannot be '.' or '..'.",
                    nameof(options));
            }

            if (options.RuntimeDirectoryName.IndexOfAny(
                    new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
            {
                throw new ArgumentException(
                    "The runtime directory name cannot contain directory separators.",
                    nameof(options));
            }

            if (options.MaximumPacketLength < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "The maximum packet length must be positive.");
            }

            if (options.IdentityProvider is null)
            {
                throw new ArgumentException("An SSH identity provider is required.", nameof(options));
            }
        }
    }
}
