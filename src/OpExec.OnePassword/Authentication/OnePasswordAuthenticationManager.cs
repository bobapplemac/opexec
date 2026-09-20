// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        OnePasswordAuthenticationManager.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Selects an inherited 1Password session or performs interactive sign-in, validates
//              the resulting account-scoped session, and returns a disposable process-scoped
//              authentication environment.
// ------------------------------------------------------------------------------------------

namespace OpExec.OnePassword
{
    public sealed class OnePasswordAuthenticationManager : IOnePasswordAuthenticationManager
    {
        private readonly IOnePasswordAuthenticationProbe _authenticationProbe;
        private readonly IInteractiveOnePasswordSignInRunner _signInRunner;
        private readonly Action<string>? _status;
        private readonly Action<string>? _log;

        public OnePasswordAuthenticationManager(
            IOnePasswordAuthenticationProbe authenticationProbe,
            IInteractiveOnePasswordSignInRunner signInRunner,
            Action<string>? status = null,
            Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(authenticationProbe);
            ArgumentNullException.ThrowIfNull(signInRunner);

            _authenticationProbe = authenticationProbe;
            _signInRunner = signInRunner;
            _status = status;
            _log = log;
        }

        public async Task<IOnePasswordAuthenticationContext> EstablishAsync(
            CancellationToken cancellationToken)
        {
            var inheritedEnvironment = new Dictionary<string, string?>();

            if (await _authenticationProbe.IsAuthenticatedAsync(
                inheritedEnvironment,
                cancellationToken))
            {
                _log?.Invoke("authentication source: inherited");
                return new OnePasswordAuthenticationContext();
            }

            var accounts = await _authenticationProbe.ListAccountsAsync(
                cancellationToken);

            if (accounts.Count == 0)
            {
                throw new InvalidOperationException(
                    "No 1Password CLI accounts are configured. Run 'op account add' first.");
            }

            _status?.Invoke("1Password authentication required.");
            var signIn = await _signInRunner.RunAsync(cancellationToken);

            if (signIn.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"The 1Password CLI sign-in failed with exit code {signIn.ExitCode}.");
            }

            var sessionToken = signIn.SessionToken.TrimEnd('\r', '\n');

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidDataException(
                    "The 1Password CLI sign-in returned no session token.");
            }

            if (sessionToken.Contains('\r') || sessionToken.Contains('\n'))
            {
                throw new InvalidDataException(
                    "The 1Password CLI sign-in returned an invalid session token.");
            }

            foreach (var account in accounts)
            {
                var environmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["OP_ACCOUNT"] = account.AccountId,
                    ["OP_CONNECT_HOST"] = null,
                    ["OP_CONNECT_TOKEN"] = null,
                    ["OP_SERVICE_ACCOUNT_TOKEN"] = null,
                    ["OP_SERVICE_ACCOUNT_TOKEN_FILE"] = null,
                    ["OP_SESSION"] = null,
                    [$"OP_SESSION_{account.UserId}"] = sessionToken
                };
                var authentication = new OnePasswordAuthenticationContext(
                    environmentVariables);

                if (!await _authenticationProbe.IsAuthenticatedAsync(
                    authentication.EnvironmentVariables,
                    cancellationToken))
                {
                    authentication.Dispose();
                    continue;
                }

                _log?.Invoke("authentication source: created");
                return authentication;
            }

            throw new InvalidOperationException(
                "The 1Password CLI did not accept the newly created session for any configured account.");
        }
    }
}
