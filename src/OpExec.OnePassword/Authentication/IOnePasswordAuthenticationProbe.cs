// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public interface IOnePasswordAuthenticationProbe
    {
        Task<IReadOnlyList<OnePasswordAccount>> ListAccountsAsync(
            CancellationToken cancellationToken);

        Task<bool> IsAuthenticatedAsync(
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken);
    }
}
