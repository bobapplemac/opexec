// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class AgentStopRunner
    {
        private readonly AgentDaemonController _controller;

        public AgentStopRunner(AgentDaemonController controller)
        {
            ArgumentNullException.ThrowIfNull(controller);
            _controller = controller;
        }

        public async Task<int> RunAsync(
            InvocationRequest invocation,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            int stopped;

            if (invocation.Action == InvocationAction.StopAllAgents)
            {
                stopped = await _controller.StopAllAsync(log, cancellationToken);
            }
            else
            {
                stopped = await _controller.StopCurrentAsync(
                    Environment.GetEnvironmentVariable("SSH_AUTH_SOCK"),
                    log,
                    cancellationToken);
            }

            Console.Out.WriteLine("unset SSH_AUTH_SOCK OPSSH_AGENT_PID");
            Console.Out.WriteLine(
                stopped == 0
                    ? "# No detached opssh agents were running."
                    : $"# Stopped {stopped} detached opssh agent(s).");
            return 0;
        }
    }
}
