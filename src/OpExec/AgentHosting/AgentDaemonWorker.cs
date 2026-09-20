// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace OpExec
{
    internal static class AgentDaemonWorker
    {
        public const string InternalArgument = "--internal-opssh-agent-worker";

        public static bool IsInvocation(IReadOnlyList<string> arguments)
        {
            return arguments.Count > 0 && arguments[0] == InternalArgument;
        }

        public static async Task<int> RunAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            var verbose = arguments.Skip(1).Any(argument => argument == "--verbose");
            AgentDaemonLog? log = null;
            var readinessReported = false;

            try
            {
                CreateSession();
                log = new AgentDaemonLog(verbose);
                using var stopRequested = new CancellationTokenSource();
                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    stopRequested.Token);
                var result = await AgentHost.RunAsync(
                    new Dictionary<string, string?>(),
                    log.WriteVerbose,
                    socketPath =>
                    {
                        var controlServer = AgentDaemonControlServer.Start(
                            socketPath,
                            stopRequested.Cancel,
                            log.WriteVerbose,
                            lifetime.Token);
                        Console.Out.WriteLine(
                            AgentDaemonProtocol.CreateReady(socketPath, log.Path));
                        Console.Out.Flush();
                        readinessReported = true;
                        return controlServer;
                    },
                    log.WriteFailure,
                    lifetime.Token);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }
            catch (Exception exception)
            {
                if (!readinessReported)
                {
                    Console.Out.WriteLine(
                        AgentDaemonProtocol.CreateError(exception.Message));
                    Console.Out.Flush();
                }
                else
                {
                    log?.WriteFailure(exception.Message);
                }

                return 1;
            }
            finally
            {
                log?.Dispose();
            }
        }

        private static void CreateSession()
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Detached opssh agent mode is currently supported only on Linux.");
            }

            if (setsid() < 0)
            {
                throw new InvalidOperationException(
                    $"Unable to detach the opssh agent process (errno {Marshal.GetLastPInvokeError()}).");
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int setsid();
    }
}
