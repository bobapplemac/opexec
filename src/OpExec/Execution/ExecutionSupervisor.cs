// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        ExecutionSupervisor.cs
// Revision:    r4
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Coordinates the scoped 1Password context, ephemeral integrated SSH agent,
//              foreground child environment, 1Password session keepalive, cancellation, and
//              deterministic cleanup for an opexec command or interactive shell invocation.
// ------------------------------------------------------------------------------------------

using OpExec.OnePassword;

namespace OpExec
{
    internal sealed class ExecutionSupervisor
    {
        private const string AuthenticationExpiredMessage =
            "1Password authentication expired or is no longer valid. " +
            "Future SSH signing requests may fail; restart this opexec session.";

        private readonly IIntegratedSshAgentFactory _agentFactory;
        private readonly IForegroundProcessRunner _processRunner;
        private readonly IOnePasswordAuthenticationManager _authenticationManager;

        public ExecutionSupervisor(
            IOnePasswordAuthenticationManager authenticationManager,
            IIntegratedSshAgentFactory agentFactory,
            IForegroundProcessRunner processRunner)
        {
            ArgumentNullException.ThrowIfNull(authenticationManager);
            ArgumentNullException.ThrowIfNull(agentFactory);
            ArgumentNullException.ThrowIfNull(processRunner);

            _authenticationManager = authenticationManager;
            _agentFactory = agentFactory;
            _processRunner = processRunner;
        }

        public async Task<int> RunAsync(
            ForegroundProcessRequest request,
            Action<string>? log,
            CancellationToken cancellationToken,
            Action<string>? failure = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var authentication = await _authenticationManager.EstablishAsync(
                cancellationToken);
            await using var agent = await _agentFactory.StartAsync(
                authentication.EnvironmentVariables,
                log,
                cancellationToken);
            await using var keepAlive = OnePasswordSessionKeepAlive.Start(
                authentication.EnvironmentVariables,
                log,
                () => failure?.Invoke(AuthenticationExpiredMessage),
                cancellationToken);
            var scopedRequest = request
                .WithEnvironmentVariables(authentication.EnvironmentVariables)
                .WithEnvironmentVariable("SSH_AUTH_SOCK", agent.SocketPath);
            log?.Invoke($"starting child executable: {request.ExecutablePath}");
            return await _processRunner.RunAsync(scopedRequest, cancellationToken);
        }
    }
}
