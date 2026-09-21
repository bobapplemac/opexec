// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace OpExec.Tests
{
    public sealed class AgentDaemonProtocolTests
    {
        [Fact]
        public void ReadyMessageRoundTripsPathsWithoutExposingThemDirectly()
        {
            const string socketPath = "/run/user/1000/opexec path/agent.sock";
            const string logPath = "/home/user/.local/state/opexec/agent.log";

            var message = AgentDaemonProtocol.CreateReady(socketPath, logPath);
            var parsed = AgentDaemonProtocol.TryParseReady(
                message,
                out var parsedSocketPath,
                out var parsedLogPath);

            Assert.True(parsed);
            Assert.Equal(socketPath, parsedSocketPath);
            Assert.Equal(logPath, parsedLogPath);
            Assert.DoesNotContain(socketPath, message);
        }

        [Fact]
        public void ErrorMessageRoundTrips()
        {
            const string error = "agent startup failed";

            var message = AgentDaemonProtocol.CreateError(error);

            Assert.True(AgentDaemonProtocol.TryParseError(message, out var parsed));
            Assert.Equal(error, parsed);
        }

        [Theory]
        [InlineData("")]
        [InlineData("READY")]
        [InlineData("READY\tnot-base64\tstill-not-base64")]
        [InlineData("unexpected")]
        public void InvalidReadyMessageIsRejected(string message)
        {
            Assert.False(
                AgentDaemonProtocol.TryParseReady(message, out _, out _));
        }

        [Fact]
        public void ShellQuoteProtectsApostrophesAndWhitespace()
        {
            var result = AgentModeRunner.ShellQuote("/tmp/a path/it's.sock");

            Assert.Equal("'/tmp/a path/it'\"'\"'s.sock'", result);
        }

        [Fact]
        public void DaemonExportIncludesSocketAndNamespacedProcessId()
        {
            var daemon = new AgentDaemonStartResult(
                12345,
                "/run/user/1000/opexec path/agent.sock",
                "/tmp/log",
                false);

            var result = AgentModeRunner.CreateDaemonExport(daemon);

            Assert.Equal(
                "export SSH_AUTH_SOCK='/run/user/1000/opexec path/agent.sock' " +
                "OPSSH_AGENT_PID='12345'",
                result);
        }

        [Fact]
        public void ControlProtocolCreatesAndParsesJsonRpcStopRequest()
        {
            var message = AgentDaemonControlProtocol.CreateStopRequest();
            var parsed = AgentDaemonControlProtocol.ParseStopRequest(message);

            Assert.True(parsed.ShouldStop);
            Assert.Equal(1, parsed.Id);
            Assert.Contains("\"jsonrpc\":\"2.0\"", Encoding.UTF8.GetString(message));
            Assert.Contains("\"method\":\"agent.stop\"", Encoding.UTF8.GetString(message));
        }

        [Theory]
        [InlineData("not-json", -32700)]
        [InlineData("{}", -32600)]
        [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"unknown\",\"id\":7}", -32601)]
        [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"agent.stop\",\"params\":[],\"id\":7}", -32602)]
        public void ControlProtocolReturnsStandardJsonRpcErrors(
            string message,
            int expectedCode)
        {
            var parsed = AgentDaemonControlProtocol.ParseStopRequest(
                Encoding.UTF8.GetBytes(message));

            Assert.False(parsed.ShouldStop);
            Assert.Equal(expectedCode, parsed.ErrorCode);
        }

        [Fact]
        public async Task ControlProtocolFramesJsonWithBigEndianUInt32Length()
        {
            var message = AgentDaemonControlProtocol.CreateStopRequest();
            await using var stream = new MemoryStream();

            await AgentDaemonControlProtocol.WriteFrameAsync(
                stream,
                message,
                CancellationToken.None);

            var framed = stream.ToArray();
            Assert.Equal(
                checked((uint)message.Length),
                BinaryPrimitives.ReadUInt32BigEndian(framed.AsSpan(0, 4)));
            stream.Position = 0;
            var decoded = await AgentDaemonControlProtocol.ReadFrameAsync(
                stream,
                CancellationToken.None);
            Assert.Equal(message, decoded);
        }

        [Fact]
        public async Task ControlProtocolRejectsOversizedFrameBeforeAllocation()
        {
            var header = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(header, 4097);
            await using var stream = new MemoryStream(header);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => AgentDaemonControlProtocol.ReadFrameAsync(
                    stream,
                    CancellationToken.None));
        }

        [Fact]
        public void ControlProtocolValidatesCorrelatedSuccessResponse()
        {
            var response = AgentDaemonControlProtocol.CreateSuccessResponse(1);

            AgentDaemonControlProtocol.ValidateSuccessResponse(response);

            var wrongId = AgentDaemonControlProtocol.CreateSuccessResponse(2);
            Assert.Throws<InvalidDataException>(
                () => AgentDaemonControlProtocol.ValidateSuccessResponse(wrongId));
        }

        [Fact]
        public void ControlSocketIsPlacedBesideAgentSocket()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "opssh",
                "instance");
            var result = AgentDaemonControlProtocol.GetControlSocketPath(
                Path.Combine(directory, "agent.sock"));

            Assert.Equal(
                Path.Combine(directory, "control.sock"),
                result);
        }

        [Fact]
        public async Task ControlServerAcceptsStopAndRemovesItsSocket()
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            var directory = Path.Combine(
                Path.GetTempPath(),
                $"opssh-control-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var agentSocketPath = Path.Combine(directory, "agent.sock");
            var controlSocketPath = Path.Combine(directory, "control.sock");
            var stopped = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                await using (var server = AgentDaemonControlServer.Start(
                                 agentSocketPath,
                                 () => stopped.TrySetResult(),
                                 null,
                                 CancellationToken.None))
                {
                    Assert.Equal(
                        UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        File.GetUnixFileMode(controlSocketPath));

                    using var client = new Socket(
                        AddressFamily.Unix,
                        SocketType.Stream,
                        ProtocolType.Unspecified);
                    await client.ConnectAsync(
                        new UnixDomainSocketEndPoint(controlSocketPath));
                    await using var stream = new NetworkStream(
                        client,
                        ownsSocket: false);
                    var request = AgentDaemonControlProtocol.CreateStopRequest();
                    await AgentDaemonControlProtocol.WriteFrameAsync(
                        stream,
                        request,
                        CancellationToken.None);
                    var response = await AgentDaemonControlProtocol.ReadFrameAsync(
                        stream,
                        CancellationToken.None);

                    AgentDaemonControlProtocol.ValidateSuccessResponse(response);
                    await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }

                Assert.False(File.Exists(controlSocketPath));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
