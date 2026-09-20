// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        AgentHost.cs
// Revision:    r4
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Hosts the 1Password-backed SSH agent shared by foreground and detached agent
//              modes, including identity-provider creation, session keepalive, authentication
//              invalidation, readiness lifetime, cancellation, and cleanup.
// ------------------------------------------------------------------------------------------

using OpExec.OnePassword;
using OpExec.SshAgent;
using SshAgentHost = OpExec.SshAgent.SshAgent;

namespace OpExec
{
    internal static class AgentHost
    {
        public const string AuthenticationExpiredMessage =
            "1Password authentication expired or is no longer valid. " +
            "Sign in again and restart the opssh agent.";

        public static async Task<int> RunAsync(
            IReadOnlyDictionary<string, string?> environmentVariables,
            Action<string>? log,
            Func<string, IAsyncDisposable?> ready,
            Action<string> failure,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(environmentVariables);
            ArgumentNullException.ThrowIfNull(ready);
            ArgumentNullException.ThrowIfNull(failure);

            var authenticationInvalid = 0;
            using var authenticationInvalidated = new CancellationTokenSource();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                authenticationInvalidated.Token);

            void AuthenticationInvalidated()
            {
                if (Interlocked.Exchange(ref authenticationInvalid, 1) == 0)
                {
                    try
                    {
                        failure(AuthenticationExpiredMessage);
                    }
                    finally
                    {
                        authenticationInvalidated.Cancel();
                    }
                }
            }

            var client = new OnePasswordClient(
                new OnePasswordClientOptions
                {
                    EnvironmentVariables = environmentVariables
                });
            var identityProvider = await OnePasswordIdentityProvider.CreateAsync(
                client,
                log,
                lifetime.Token,
                AuthenticationInvalidated);
            await using var agent = await SshAgentHost.StartAsync(
                new SshAgentOptions
                {
                    RuntimeDirectoryName = "opssh",
                    IdentityProvider = identityProvider,
                    Log = log
                },
                lifetime.Token);
            await using var keepAlive = OnePasswordSessionKeepAlive.Start(
                environmentVariables,
                log,
                AuthenticationInvalidated,
                lifetime.Token);

            var readinessScope = ready(agent.SocketPath);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            finally
            {
                if (readinessScope is not null)
                {
                    await readinessScope.DisposeAsync();
                }
            }

            return Volatile.Read(ref authenticationInvalid) == 0 ? 0 : 1;
        }
    }
}
