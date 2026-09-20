// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public sealed class OnePasswordAuthenticationProbe : IOnePasswordAuthenticationProbe
    {
        public Task<IReadOnlyList<OnePasswordAccount>> ListAccountsAsync(
            CancellationToken cancellationToken)
        {
            return new OnePasswordClient().ListAccountsAsync(cancellationToken);
        }

        public Task<bool> IsAuthenticatedAsync(
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(environmentVariables);

            return new OnePasswordClient(
                new OnePasswordClientOptions
                {
                    EnvironmentVariables = environmentVariables
                }).IsAuthenticatedAsync(cancellationToken);
        }
    }
}
