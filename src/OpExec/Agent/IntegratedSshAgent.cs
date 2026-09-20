// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.SshAgent;
using SshAgentHost = OpExec.SshAgent.SshAgent;

namespace OpExec
{
    internal sealed class IntegratedSshAgent : IIntegratedSshAgent
    {
        private readonly SshAgentHost _agent;

        public IntegratedSshAgent(SshAgentHost agent)
        {
            ArgumentNullException.ThrowIfNull(agent);
            _agent = agent;
        }

        public string SocketPath => _agent.SocketPath;

        public ValueTask DisposeAsync()
        {
            return _agent.DisposeAsync();
        }
    }
}
