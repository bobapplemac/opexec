// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Net.Sockets;
using System.Buffers.Binary;
using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class SshAgentIntegrationTests
    {
        [Fact]
        public async Task ServerHandlesConnectionsAndRemovesRuntimeDirectoryOnShutdown()
        {
            Assert.SkipUnless(
                Socket.OSSupportsUnixDomainSockets,
                "SSH-agent integration requires Unix-domain socket support.");

            var testRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"oat-{Guid.NewGuid():N}"[..12]);
            Directory.CreateDirectory(testRoot);
            string? runtimeDirectory = null;

            try
            {
                await using (var agent = await SshAgent.StartAsync(
                                 new SshAgentOptions
                                 {
                                     RuntimeBaseDirectory = testRoot,
                                     RuntimeDirectoryName = "a",
                                     IdentityProvider = new TestIdentityProvider()
                                 },
                                 TestContext.Current.CancellationToken))
                {
                    runtimeDirectory = System.IO.Path.GetDirectoryName(agent.SocketPath);
                    Assert.True(Directory.Exists(runtimeDirectory));

                    if (!OperatingSystem.IsWindows())
                    {
                        Assert.Equal(
                            UnixFileMode.UserRead |
                            UnixFileMode.UserWrite |
                            UnixFileMode.UserExecute,
                            File.GetUnixFileMode(runtimeDirectory!));
                        Assert.Equal(
                            UnixFileMode.UserRead | UnixFileMode.UserWrite,
                            File.GetUnixFileMode(agent.SocketPath));
                    }

                    using var client = new Socket(
                        AddressFamily.Unix,
                        SocketType.Stream,
                        ProtocolType.Unspecified);
                    await client.ConnectAsync(
                        new UnixDomainSocketEndPoint(agent.SocketPath),
                        TestContext.Current.CancellationToken);
                    await using var stream = new NetworkStream(client, ownsSocket: false);

                    await AgentPacket.WriteAsync(
                        stream,
                        new byte[] { AgentMessageType.RequestIdentities },
                        CancellationToken.None);
                    var identityResponse = await AgentPacket.ReadAsync(
                        stream,
                        SshAgentOptions.DefaultMaximumPacketLength,
                        CancellationToken.None);
                    Assert.NotNull(identityResponse);
                    AgentProtocolTests.AssertTestIdentityResponse(identityResponse!);

                    await AgentPacket.WriteAsync(
                        stream,
                        new byte[] { 255 },
                        CancellationToken.None);
                    var firstResponse = await AgentPacket.ReadAsync(
                        stream,
                        SshAgentOptions.DefaultMaximumPacketLength,
                        CancellationToken.None);
                    await AgentPacket.WriteAsync(
                        stream,
                        new byte[] { 254 },
                        CancellationToken.None);
                    var secondResponse = await AgentPacket.ReadAsync(
                        stream,
                        SshAgentOptions.DefaultMaximumPacketLength,
                        CancellationToken.None);

                    AssertFailure(firstResponse);
                    AssertFailure(secondResponse);

                    var concurrentClients = Enumerable.Range(0, 4)
                        .Select(_ => SendUnsupportedRequestAsync(agent.SocketPath));
                    await Task.WhenAll(concurrentClients);

                    using (var emptyPacketClient = new Socket(
                               AddressFamily.Unix,
                               SocketType.Stream,
                               ProtocolType.Unspecified))
                    {
                        await emptyPacketClient.ConnectAsync(
                            new UnixDomainSocketEndPoint(agent.SocketPath),
                            TestContext.Current.CancellationToken);
                        await using var emptyPacketStream = new NetworkStream(
                            emptyPacketClient,
                            ownsSocket: false);
                        await emptyPacketStream.WriteAsync(
                            new byte[sizeof(uint)],
                            TestContext.Current.CancellationToken);
                    }

                    await SendUnsupportedRequestAsync(agent.SocketPath);

                    using (var malformedClient = new Socket(
                               AddressFamily.Unix,
                               SocketType.Stream,
                               ProtocolType.Unspecified))
                    {
                        await malformedClient.ConnectAsync(
                            new UnixDomainSocketEndPoint(agent.SocketPath),
                            TestContext.Current.CancellationToken);
                        await using var malformedStream = new NetworkStream(
                            malformedClient,
                            ownsSocket: false);
                        var oversizedHeader = new byte[sizeof(uint)];
                        BinaryPrimitives.WriteUInt32BigEndian(
                            oversizedHeader,
                            SshAgentOptions.DefaultMaximumPacketLength + 1U);
                        await malformedStream.WriteAsync(
                            oversizedHeader,
                            TestContext.Current.CancellationToken);
                    }

                    await SendUnsupportedRequestAsync(agent.SocketPath);
                }

                Assert.False(Directory.Exists(runtimeDirectory));
            }
            finally
            {
                var agentRoot = System.IO.Path.Combine(testRoot, "a");

                if (Directory.Exists(agentRoot))
                {
                    Directory.Delete(agentRoot, recursive: false);
                }

                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: false);
                }
            }
        }

        [Fact]
        public async Task OversizedResponseBecomesFailureAndServerRemainsAvailable()
        {
            Assert.SkipUnless(
                Socket.OSSupportsUnixDomainSockets,
                "SSH-agent integration requires Unix-domain socket support.");

            var testRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"oal-{Guid.NewGuid():N}"[..12]);
            Directory.CreateDirectory(testRoot);

            try
            {
                await using var agent = await SshAgent.StartAsync(
                    new SshAgentOptions
                    {
                        RuntimeBaseDirectory = testRoot,
                        RuntimeDirectoryName = "a",
                        MaximumPacketLength = 32,
                        IdentityProvider = new TestIdentityProvider()
                    },
                    TestContext.Current.CancellationToken);

                await AssertRequestReturnsFailureAsync(
                    agent.SocketPath,
                    new byte[] { AgentMessageType.RequestIdentities },
                    32);
                await AssertRequestReturnsFailureAsync(
                    agent.SocketPath,
                    new byte[] { 255 },
                    32);
            }
            finally
            {
                var agentRoot = System.IO.Path.Combine(testRoot, "a");

                if (Directory.Exists(agentRoot))
                {
                    Directory.Delete(agentRoot, recursive: false);
                }

                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: false);
                }
            }
        }

        [Fact]
        public async Task ProviderFailureLoggingDoesNotExposeExceptionMessage()
        {
            Assert.SkipUnless(
                Socket.OSSupportsUnixDomainSockets,
                "SSH-agent integration requires Unix-domain socket support.");

            var testRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"oaf-{Guid.NewGuid():N}"[..12]);
            Directory.CreateDirectory(testRoot);
            var logs = new List<string>();

            try
            {
                await using var agent = await SshAgent.StartAsync(
                    new SshAgentOptions
                    {
                        RuntimeBaseDirectory = testRoot,
                        RuntimeDirectoryName = "a",
                        IdentityProvider = new FailingIdentityProvider(),
                        Log = logs.Add
                    },
                    TestContext.Current.CancellationToken);

                await AssertRequestReturnsFailureAsync(
                    agent.SocketPath,
                    new byte[] { AgentMessageType.RequestIdentities },
                    SshAgentOptions.DefaultMaximumPacketLength);

                Assert.Contains(
                    logs,
                    message => message.Contains(nameof(InvalidOperationException)));
                Assert.DoesNotContain(
                    logs,
                    message => message.Contains("sensitive-provider-detail"));
            }
            finally
            {
                var agentRoot = System.IO.Path.Combine(testRoot, "a");

                if (Directory.Exists(agentRoot))
                {
                    Directory.Delete(agentRoot, recursive: false);
                }

                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: false);
                }
            }
        }

        [Fact]
        public async Task StartupFailureRemovesPartiallyCreatedRuntimeDirectory()
        {
            Assert.SkipUnless(
                Socket.OSSupportsUnixDomainSockets,
                "SSH-agent integration requires Unix-domain socket support.");

            var testRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"oas-{Guid.NewGuid():N}"[..12]);
            Directory.CreateDirectory(testRoot);

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => SshAgent.StartAsync(
                        new SshAgentOptions
                        {
                            RuntimeBaseDirectory = testRoot,
                            RuntimeDirectoryName = "a",
                            IdentityProvider = new TestIdentityProvider(),
                            Log = message =>
                            {
                                if (message.StartsWith(
                                    "runtime directory created:",
                                    StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException(
                                        "test startup failure");
                                }
                            }
                        },
                        TestContext.Current.CancellationToken));

                var agentRoot = System.IO.Path.Combine(testRoot, "a");
                Assert.Empty(Directory.EnumerateFileSystemEntries(agentRoot));
            }
            finally
            {
                var agentRoot = System.IO.Path.Combine(testRoot, "a");

                if (Directory.Exists(agentRoot))
                {
                    Directory.Delete(agentRoot, recursive: false);
                }

                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: false);
                }
            }
        }

        [Fact]
        public async Task ShutdownFailureStillRemovesRuntimeDirectory()
        {
            Assert.SkipUnless(
                Socket.OSSupportsUnixDomainSockets,
                "SSH-agent integration requires Unix-domain socket support.");

            var testRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"oad-{Guid.NewGuid():N}"[..12]);
            Directory.CreateDirectory(testRoot);
            string? runtimeDirectory = null;

            try
            {
                var agent = await SshAgent.StartAsync(
                    new SshAgentOptions
                    {
                        RuntimeBaseDirectory = testRoot,
                        RuntimeDirectoryName = "a",
                        IdentityProvider = new TestIdentityProvider(),
                        Log = message =>
                        {
                            if (message == "agent server stopped")
                            {
                                throw new InvalidOperationException(
                                    "test shutdown failure");
                            }
                        }
                    },
                    TestContext.Current.CancellationToken);
                runtimeDirectory = System.IO.Path.GetDirectoryName(agent.SocketPath);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => agent.DisposeAsync().AsTask());

                Assert.False(Directory.Exists(runtimeDirectory));
            }
            finally
            {
                if (runtimeDirectory is not null && Directory.Exists(runtimeDirectory))
                {
                    Directory.Delete(runtimeDirectory, recursive: false);
                }

                var agentRoot = System.IO.Path.Combine(testRoot, "a");

                if (Directory.Exists(agentRoot))
                {
                    Directory.Delete(agentRoot, recursive: false);
                }

                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: false);
                }
            }
        }

        private static async Task SendUnsupportedRequestAsync(string socketPath)
        {
            using var client = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
            await using var stream = new NetworkStream(client, ownsSocket: false);
            await AgentPacket.WriteAsync(
                stream,
                new byte[] { 255 },
                CancellationToken.None);
            var response = await AgentPacket.ReadAsync(
                stream,
                SshAgentOptions.DefaultMaximumPacketLength,
                CancellationToken.None);

            AssertFailure(response);
        }

        private static async Task AssertRequestReturnsFailureAsync(
            string socketPath,
            byte[] request,
            int maximumPacketLength)
        {
            using var client = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
            await using var stream = new NetworkStream(client, ownsSocket: false);
            await AgentPacket.WriteAsync(stream, request, CancellationToken.None);
            var response = await AgentPacket.ReadAsync(
                stream,
                maximumPacketLength,
                CancellationToken.None);
            AssertFailure(response);
        }

        private static void AssertFailure(byte[]? response)
        {
            Assert.Equal(new byte[] { AgentMessageType.Failure }, response);
        }

        private sealed class FailingIdentityProvider : ISshIdentityProvider
        {
            public Task<IReadOnlyList<SshIdentity>> GetIdentitiesAsync(
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("sensitive-provider-detail");
            }

            public Task<SshSignature> SignAsync(
                SshIdentity identity,
                ReadOnlyMemory<byte> data,
                uint flags,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("sensitive-provider-detail");
            }
        }
    }
}
