// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        AgentDaemonControlServer.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Exposes the private mode-0600 Unix-domain control socket used to stop detached
//              opssh agents through the bounded length-framed JSON-RPC protocol.
// ------------------------------------------------------------------------------------------

using System.Net.Sockets;

namespace OpExec
{
    internal sealed class AgentDaemonControlServer : IAsyncDisposable
    {
        private readonly string _socketPath;
        private readonly Socket _listener;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _acceptLoop;
        private bool _disposed;

        private AgentDaemonControlServer(
            string socketPath,
            Socket listener,
            Action stop,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            _socketPath = socketPath;
            _listener = listener;
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdown.Token);
            _acceptLoop = AcceptLoopAsync(listener, stop, log, lifetime);
        }

        public static AgentDaemonControlServer Start(
            string agentSocketPath,
            Action stop,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(agentSocketPath);
            ArgumentNullException.ThrowIfNull(stop);

            var socketPath = AgentDaemonControlProtocol.GetControlSocketPath(
                agentSocketPath);
            var listener = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);

            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(socketPath));
                RestrictSocketAccess(socketPath);
                listener.Listen(4);
                log?.Invoke($"daemon control socket listening: {socketPath}");
                return new AgentDaemonControlServer(
                    socketPath,
                    listener,
                    stop,
                    log,
                    cancellationToken);
            }
            catch
            {
                listener.Dispose();
                TryDeleteSocket(socketPath);
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
            _shutdown.Cancel();
            _listener.Dispose();

            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _shutdown.Dispose();
                TryDeleteSocket(_socketPath);
            }
        }

        private static async Task AcceptLoopAsync(
            Socket listener,
            Action stop,
            Action<string>? log,
            CancellationTokenSource lifetime)
        {
            using (lifetime)
            {
                while (!lifetime.IsCancellationRequested)
                {
                    Socket client;

                    try
                    {
                        client = await listener.AcceptAsync(lifetime.Token);
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ObjectDisposedException) when (lifetime.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (SocketException) when (lifetime.IsCancellationRequested)
                    {
                        break;
                    }

                    _ = HandleClientAsync(client, stop, log, lifetime.Token);
                }
            }
        }

        private static async Task HandleClientAsync(
            Socket client,
            Action stop,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            using (client)
            await using (var stream = new NetworkStream(client, ownsSocket: false))
            {
                byte[]? request = null;

                try
                {
                    request = await AgentDaemonControlProtocol.ReadFrameAsync(
                        stream,
                        cancellationToken);
                    var parsed = AgentDaemonControlProtocol.ParseStopRequest(
                        request);

                    if (!parsed.ShouldStop)
                    {
                        var error = AgentDaemonControlProtocol.CreateErrorResponse(
                            parsed.Id,
                            parsed.ErrorCode!.Value,
                            parsed.ErrorMessage!);

                        try
                        {
                            await AgentDaemonControlProtocol.WriteFrameAsync(
                                stream,
                                error,
                                cancellationToken);
                        }
                        finally
                        {
                            Array.Clear(error);
                        }

                        log?.Invoke(
                            $"daemon JSON-RPC request rejected: {parsed.ErrorMessage}");
                        return;
                    }

                    var response = AgentDaemonControlProtocol.CreateSuccessResponse(
                        parsed.Id!.Value);

                    try
                    {
                        await AgentDaemonControlProtocol.WriteFrameAsync(
                            stream,
                            response,
                            cancellationToken);
                    }
                    finally
                    {
                        Array.Clear(response);
                    }

                    log?.Invoke("daemon stop requested by JSON-RPC");
                    stop();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (IOException exception)
                {
                    log?.Invoke($"daemon control I/O ended: {exception.Message}");
                }
                catch (SocketException exception)
                {
                    log?.Invoke($"daemon control socket ended: {exception.Message}");
                }
                finally
                {
                    if (request is not null)
                    {
                        Array.Clear(request);
                    }
                }
            }
        }

        private static void RestrictSocketAccess(string socketPath)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            const UnixFileMode expectedMode =
                UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(socketPath, expectedMode);
            var actualMode = File.GetUnixFileMode(socketPath) &
                (UnixFileMode.UserRead |
                 UnixFileMode.UserWrite |
                 UnixFileMode.UserExecute |
                 UnixFileMode.GroupRead |
                 UnixFileMode.GroupWrite |
                 UnixFileMode.GroupExecute |
                 UnixFileMode.OtherRead |
                 UnixFileMode.OtherWrite |
                 UnixFileMode.OtherExecute);

            if (actualMode != expectedMode)
            {
                throw new UnauthorizedAccessException(
                    "The daemon control socket permissions could not be restricted to 0600.");
            }
        }

        private static void TryDeleteSocket(string socketPath)
        {
            try
            {
                if (File.Exists(socketPath))
                {
                    File.Delete(socketPath);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
