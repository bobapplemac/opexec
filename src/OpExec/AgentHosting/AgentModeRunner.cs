// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.OnePassword;

namespace OpExec
{
    internal sealed class AgentModeRunner
    {
        private readonly IOnePasswordAuthenticationManager _authenticationManager;
        private readonly AgentDaemonLauncher _daemonLauncher;

        public AgentModeRunner(
            IOnePasswordAuthenticationManager authenticationManager,
            AgentDaemonLauncher daemonLauncher)
        {
            ArgumentNullException.ThrowIfNull(authenticationManager);
            ArgumentNullException.ThrowIfNull(daemonLauncher);

            _authenticationManager = authenticationManager;
            _daemonLauncher = daemonLauncher;
        }

        public async Task<int> RunAsync(
            InvocationRequest invocation,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            if (invocation.Daemon && !OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Detached opssh agent mode is currently supported only on Linux.");
            }

            using var authentication = await _authenticationManager.EstablishAsync(
                cancellationToken);

            if (invocation.Daemon)
            {
                var daemon = await _daemonLauncher.StartAsync(
                    authentication.EnvironmentVariables,
                    invocation.Verbose,
                    cancellationToken);
                WriteDaemonExports(daemon);
                return 0;
            }

            return await AgentHost.RunAsync(
                authentication.EnvironmentVariables,
                log,
                socketPath =>
                {
                    Console.Out.WriteLine(
                        $"export SSH_AUTH_SOCK={ShellQuote(socketPath)}");
                    Console.Out.WriteLine(
                        "# opssh agent is running in the foreground. Press Ctrl+C to stop it.");
                    Console.Out.Flush();
                    return null;
                },
                message => Console.Error.WriteLine($"opssh: {message}"),
                cancellationToken);
        }

        private static void WriteDaemonExports(AgentDaemonStartResult daemon)
        {
            Console.Out.WriteLine(CreateDaemonExport(daemon));
            Console.Out.WriteLine(
                $"# opssh agent is running in the background with PID {daemon.ProcessId}.");
            Console.Out.WriteLine(
                "# Stop it and clear this shell with: eval \"$(opssh --agent --stop)\"");
            Console.Out.WriteLine(
                "# Stop every detached opssh agent with: eval \"$(opssh --agent --stop-all)\"");
            Console.Out.WriteLine(
                daemon.Verbose
                    ? $"# Diagnostic log: {FormatCommentValue(daemon.LogPath)}"
                    : $"# Failure diagnostics, if created: {FormatCommentValue(daemon.LogPath)}");
            Console.Out.Flush();
        }

        internal static string CreateDaemonExport(AgentDaemonStartResult daemon)
        {
            ArgumentNullException.ThrowIfNull(daemon);
            return $"export SSH_AUTH_SOCK={ShellQuote(daemon.SocketPath)} " +
                $"OPSSH_AGENT_PID={ShellQuote(daemon.ProcessId.ToString())}";
        }

        internal static string ShellQuote(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
        }

        private static string FormatCommentValue(string value)
        {
            return value
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }
    }
}
