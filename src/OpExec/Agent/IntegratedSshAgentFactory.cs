// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.OnePassword;
using OpExec.SshAgent;
using SshAgentHost = OpExec.SshAgent.SshAgent;

namespace OpExec
{
    internal sealed class IntegratedSshAgentFactory : IIntegratedSshAgentFactory
    {
        public async Task<IIntegratedSshAgent> StartAsync(
            IReadOnlyDictionary<string, string?> environmentVariables,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(environmentVariables);

            var identityProvider = await OnePasswordIdentityProvider.CreateAsync(
                new OnePasswordClient(
                    new OnePasswordClientOptions
                    {
                        EnvironmentVariables = environmentVariables
                    }),
                log,
                cancellationToken);
            var agent = await SshAgentHost.StartAsync(
                new SshAgentOptions
                {
                    RuntimeDirectoryName = "opexec",
                    IdentityProvider = identityProvider,
                    Log = log
                },
                cancellationToken);
            return new IntegratedSshAgent(agent);
        }
    }
}
