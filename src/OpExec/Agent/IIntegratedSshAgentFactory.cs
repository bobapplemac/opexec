// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal interface IIntegratedSshAgentFactory
    {
        Task<IIntegratedSshAgent> StartAsync(
            IReadOnlyDictionary<string, string?> environmentVariables,
            Action<string>? log,
            CancellationToken cancellationToken);
    }
}
