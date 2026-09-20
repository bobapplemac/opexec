// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace OpExec
{
    internal sealed class AgentDaemonController
    {
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

        public async Task<int> StopCurrentAsync(
            string? agentSocketPath,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(agentSocketPath))
            {
                throw new InvalidOperationException(
                    "SSH_AUTH_SOCK does not select a detached opssh agent.");
            }

            var fullAgentSocketPath = Path.GetFullPath(agentSocketPath);

            if (!IsManagedAgentSocketPath(fullAgentSocketPath))
            {
                throw new InvalidOperationException(
                    "SSH_AUTH_SOCK does not select a detached opssh agent.");
            }

            var controlSocketPath =
                AgentDaemonControlProtocol.GetControlSocketPath(fullAgentSocketPath);
            await StopAsync(controlSocketPath, log, cancellationToken);
            return 1;
        }

        public async Task<int> StopAllAsync(
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            var stopped = 0;
            var failures = new List<Exception>();

            foreach (var controlSocketPath in FindControlSockets())
            {
                try
                {
                    await StopAsync(controlSocketPath, log, cancellationToken);
                    stopped++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                    log?.Invoke(
                        $"unable to stop daemon at {controlSocketPath}: " +
                        exception.Message);
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Stopped {stopped} opssh agent(s), but {failures.Count} could not be stopped.");
            }

            return stopped;
        }

        internal static bool IsManagedAgentSocketPath(string agentSocketPath)
        {
            if (!string.Equals(
                    Path.GetFileName(agentSocketPath),
                    "agent.sock",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var runtimeDirectory = Path.GetDirectoryName(agentSocketPath);

            if (runtimeDirectory is null)
            {
                return false;
            }

            var xdgRuntimeDirectory = Environment.GetEnvironmentVariable(
                "XDG_RUNTIME_DIR");

            if (!string.IsNullOrWhiteSpace(xdgRuntimeDirectory))
            {
                var expectedParent = Path.GetFullPath(
                    Path.Combine(xdgRuntimeDirectory, "opssh"));
                var actualParent = Path.GetDirectoryName(runtimeDirectory);

                if (string.Equals(
                        expectedParent,
                        actualParent,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var tempDirectory = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar);
            var actualTempParent = Path.GetDirectoryName(runtimeDirectory)?
                .TrimEnd(Path.DirectorySeparatorChar);
            var runtimeName = Path.GetFileName(runtimeDirectory);
            return string.Equals(
                    tempDirectory,
                    actualTempParent,
                    StringComparison.Ordinal) &&
                runtimeName.StartsWith(
                    $"opssh-{GetEffectiveUserId()}-",
                    StringComparison.Ordinal);
        }

        internal static IReadOnlyList<string> FindControlSockets()
        {
            var results = new HashSet<string>(StringComparer.Ordinal);
            var xdgRuntimeDirectory = Environment.GetEnvironmentVariable(
                "XDG_RUNTIME_DIR");

            if (!string.IsNullOrWhiteSpace(xdgRuntimeDirectory))
            {
                AddControlSockets(
                    Path.Combine(Path.GetFullPath(xdgRuntimeDirectory), "opssh"),
                    "*",
                    results);
            }

            AddControlSockets(
                Path.GetFullPath(Path.GetTempPath()),
                $"opssh-{GetEffectiveUserId()}-*",
                results);
            return results.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static void AddControlSockets(
            string parentDirectory,
            string directoryPattern,
            ISet<string> results)
        {
            if (!Directory.Exists(parentDirectory))
            {
                return;
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(
                             parentDirectory,
                             directoryPattern,
                             SearchOption.TopDirectoryOnly))
                {
                    var controlSocketPath = Path.Combine(
                        directory,
                        AgentDaemonControlProtocol.SocketFileName);

                    if (File.Exists(controlSocketPath))
                    {
                        results.Add(Path.GetFullPath(controlSocketPath));
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static async Task StopAsync(
            string controlSocketPath,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(controlSocketPath))
            {
                throw new InvalidOperationException(
                    $"No detached opssh agent is listening at {controlSocketPath}.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(OperationTimeout);
            using var socket = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);
            byte[]? request = null;
            byte[]? response = null;

            try
            {
                await socket.ConnectAsync(
                    new UnixDomainSocketEndPoint(controlSocketPath),
                    timeout.Token);
                await using var stream = new NetworkStream(
                    socket,
                    ownsSocket: false);
                request = AgentDaemonControlProtocol.CreateStopRequest();
                await AgentDaemonControlProtocol.WriteFrameAsync(
                    stream,
                    request,
                    timeout.Token);
                response = await AgentDaemonControlProtocol.ReadFrameAsync(
                    stream,
                    timeout.Token);
                AgentDaemonControlProtocol.ValidateSuccessResponse(response);

                log?.Invoke($"stop accepted by daemon at {controlSocketPath}");

                while (File.Exists(controlSocketPath))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out stopping the opssh daemon at {controlSocketPath}.");
            }
            finally
            {
                if (request is not null)
                {
                    Array.Clear(request);
                }

                if (response is not null)
                {
                    Array.Clear(response);
                }
            }
        }

        private static uint GetEffectiveUserId()
        {
            return OperatingSystem.IsWindows() ? 0 : geteuid();
        }

        [DllImport("libc")]
        private static extern uint geteuid();
    }
}
