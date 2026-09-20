// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        AgentServer.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Concurrent Unix-domain SSH-agent server with bounded packet processing, isolated
//              client handlers, cancellation-aware shutdown, permission hardening, and cleanup.
// ------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net.Sockets;

namespace OpExec.SshAgent
{
    internal sealed class AgentServer : IAsyncDisposable
    {
        private readonly string _socketPath;
        private readonly int _maximumPacketLength;
        private readonly ISshIdentityProvider _identityProvider;
        private readonly Action<string>? _log;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly ConcurrentDictionary<long, Task> _connections = new();
        private Socket? _listener;
        private Task? _acceptLoop;
        private long _nextConnectionId;

        public AgentServer(
            string socketPath,
            int maximumPacketLength,
            ISshIdentityProvider identityProvider,
            Action<string>? log)
        {
            _socketPath = socketPath;
            _maximumPacketLength = maximumPacketLength;
            _identityProvider = identityProvider;
            _log = log;
        }

        public void Start(CancellationToken cancellationToken)
        {
            if (!Socket.OSSupportsUnixDomainSockets)
            {
                throw new PlatformNotSupportedException("Unix-domain sockets are not supported on this platform.");
            }

            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
            RuntimeDirectory.RestrictSocketAccess(_socketPath);
            _listener.Listen();

            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _shutdown.Token,
                cancellationToken);
            _acceptLoop = AcceptConnectionsAsync(_listener, linkedCancellation);
            _log?.Invoke($"socket listening: {_socketPath}");
        }

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();
            _listener?.Dispose();

            try
            {
                if (_acceptLoop is not null)
                {
                    await IgnoreExpectedShutdownAsync(_acceptLoop);
                }

                await Task.WhenAll(_connections.Values);
            }
            finally
            {
                _shutdown.Dispose();
                _log?.Invoke("agent server stopped");
            }
        }

        private async Task AcceptConnectionsAsync(
            Socket listener,
            CancellationTokenSource linkedCancellation)
        {
            using (linkedCancellation)
            {
                while (!linkedCancellation.IsCancellationRequested)
                {
                    Socket client;

                    try
                    {
                        client = await listener.AcceptAsync(linkedCancellation.Token);
                    }
                    catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ObjectDisposedException) when (linkedCancellation.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (SocketException) when (linkedCancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    var connectionId = Interlocked.Increment(ref _nextConnectionId);
                    var connection = HandleConnectionAsync(client, linkedCancellation.Token);
                    _connections[connectionId] = connection;
                    _ = connection.ContinueWith(
                        completedConnection => _connections.TryRemove(connectionId, out _),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
        }

        private async Task HandleConnectionAsync(Socket client, CancellationToken cancellationToken)
        {
            using (client)
            await using (var stream = new NetworkStream(client, ownsSocket: false))
            {
                _log?.Invoke("agent client connected");

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var request = await AgentPacket.ReadAsync(
                            stream,
                            _maximumPacketLength,
                            cancellationToken);

                        if (request is null)
                        {
                            break;
                        }

                        _log?.Invoke($"agent message type received: {request[0]}");
                        byte[] response;

                        try
                        {
                            response = await AgentProtocol.HandleAsync(
                                request,
                                _identityProvider,
                                cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            _log?.Invoke(
                                $"agent request failed: {exception.GetType().Name}");
                            response = AgentProtocol.CreateFailureResponse();
                        }

                        if (response.Length > _maximumPacketLength)
                        {
                            _log?.Invoke(
                                "agent response exceeded the configured packet limit");
                            response = AgentProtocol.CreateFailureResponse();
                        }

                        LogIdentityCount(request, response);
                        await AgentPacket.WriteAsync(stream, response, cancellationToken);
                    }
                }
                catch (InvalidDataException exception)
                {
                    _log?.Invoke($"invalid agent packet: {exception.Message}");
                }
                catch (IOException exception)
                {
                    _log?.Invoke($"agent client I/O ended: {exception.Message}");
                }
                catch (SocketException exception)
                {
                    _log?.Invoke($"agent client socket ended: {exception.Message}");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                finally
                {
                    _log?.Invoke("agent client disconnected");
                }
            }
        }

        private void LogIdentityCount(ReadOnlyMemory<byte> request, ReadOnlyMemory<byte> response)
        {
            if (request.Span[0] != AgentMessageType.RequestIdentities ||
                response.Span[0] != AgentMessageType.IdentitiesAnswer)
            {
                return;
            }

            var reader = new AgentMessageReader(response.Span[1..]);

            if (reader.TryReadUInt32(out var identityCount))
            {
                _log?.Invoke($"identities returned: {identityCount}");
            }
        }

        private static async Task IgnoreExpectedShutdownAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }
}
