// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        OnePasswordSessionKeepAlive.cs
// Revision:    r7
// Modified:    2026-09-20
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Maintains a manually authenticated 1Password CLI session for the lifetime of
//              an OpExec scope. It performs account-specific authenticated heartbeats, retries
//              bounded transient failures, reports confirmed invalidation, and stops without
//              attempting unattended reauthentication.
// ------------------------------------------------------------------------------------------

using System.Collections;

namespace OpExec.OnePassword
{
    public sealed class OnePasswordSessionKeepAlive : IAsyncDisposable
    {
        private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMinutes(1);
        private const int DefaultMaximumConsecutiveFailures = 3;

        private readonly CancellationTokenSource _shutdown;
        private readonly Func<CancellationToken, Task<bool>> _heartbeat;
        private readonly Action<string>? _log;
        private readonly Action? _authenticationInvalidated;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _retryInterval;
        private readonly int _maximumConsecutiveFailures;
        private readonly Task _worker;
        private int _disposed;

        private OnePasswordSessionKeepAlive(
            bool enabled,
            Func<CancellationToken, Task<bool>> heartbeat,
            Action<string>? log,
            Action? authenticationInvalidated,
            TimeSpan initialDelay,
            TimeSpan interval,
            TimeSpan retryInterval,
            int maximumConsecutiveFailures,
            CancellationToken cancellationToken)
        {
            _heartbeat = heartbeat;
            _log = log;
            _authenticationInvalidated = authenticationInvalidated;
            _initialDelay = initialDelay;
            _interval = interval;
            _retryInterval = retryInterval;
            _maximumConsecutiveFailures = maximumConsecutiveFailures;
            _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsEnabled = enabled;
            _worker = enabled ? RunAsync(_shutdown.Token) : Task.CompletedTask;

            if (enabled)
            {
                _log?.Invoke("1Password session keepalive started");
            }
        }

        public bool IsEnabled { get; }

        public static OnePasswordSessionKeepAlive Start(
            IReadOnlyDictionary<string, string?> environmentVariables,
            Action<string>? log = null,
            Action? authenticationInvalidated = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(environmentVariables);

            var client = new OnePasswordClient(
                new OnePasswordClientOptions
                {
                    EnvironmentVariables = environmentVariables
                });
            return StartCore(
                environmentVariables,
                client.KeepSessionAliveWithDiagnosticsAsync,
                log,
                authenticationInvalidated,
                DefaultInitialDelay,
                DefaultInterval,
                DefaultRetryInterval,
                DefaultMaximumConsecutiveFailures,
                cancellationToken);
        }

        internal static OnePasswordSessionKeepAlive Start(
            IReadOnlyDictionary<string, string?> environmentVariables,
            Func<CancellationToken, Task<bool>> heartbeat,
            Action<string>? log,
            Action? authenticationInvalidated,
            TimeSpan interval,
            TimeSpan retryInterval,
            int maximumConsecutiveFailures,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(environmentVariables);
            ArgumentNullException.ThrowIfNull(heartbeat);

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }

            if (retryInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(retryInterval));
            }

            if (maximumConsecutiveFailures <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumConsecutiveFailures));
            }

            return StartCore(
                environmentVariables,
                heartbeat,
                log,
                authenticationInvalidated,
                interval,
                interval,
                retryInterval,
                maximumConsecutiveFailures,
                cancellationToken);
        }

        private static OnePasswordSessionKeepAlive StartCore(
            IReadOnlyDictionary<string, string?> environmentVariables,
            Func<CancellationToken, Task<bool>> heartbeat,
            Action<string>? log,
            Action? authenticationInvalidated,
            TimeSpan initialDelay,
            TimeSpan interval,
            TimeSpan retryInterval,
            int maximumConsecutiveFailures,
            CancellationToken cancellationToken)
        {
            return new OnePasswordSessionKeepAlive(
                HasManualSession(environmentVariables),
                heartbeat,
                log,
                authenticationInvalidated,
                initialDelay,
                interval,
                retryInterval,
                maximumConsecutiveFailures,
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await _shutdown.CancelAsync();

            try
            {
                await _worker;
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
            finally
            {
                _shutdown.Dispose();
            }
        }

        private static bool HasManualSession(
            IReadOnlyDictionary<string, string?> environmentVariables)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                if (variable.Key is string name)
                {
                    names.Add(name);
                }
            }

            names.UnionWith(environmentVariables.Keys);

            return names.Any(name =>
                IsSessionVariable(name) &&
                !string.IsNullOrWhiteSpace(
                    GetEffectiveValue(name, environmentVariables)));
        }

        private static bool IsSessionVariable(string name)
        {
            return string.Equals(name, "OP_SESSION", StringComparison.Ordinal) ||
                name.StartsWith("OP_SESSION_", StringComparison.Ordinal);
        }

        private static string? GetEffectiveValue(
            string name,
            IReadOnlyDictionary<string, string?> environmentVariables)
        {
            return environmentVariables.TryGetValue(name, out var value)
                ? value
                : Environment.GetEnvironmentVariable(name);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var consecutiveFailures = 0;
            var firstAttempt = true;

            while (true)
            {
                var delay = firstAttempt
                    ? _initialDelay
                    : consecutiveFailures == 0
                        ? _interval
                        : _retryInterval;
                await Task.Delay(delay, cancellationToken);
                firstAttempt = false;

                var succeeded = false;

                try
                {
                    succeeded = await _heartbeat(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OnePasswordHeartbeatException exception)
                {
                    _log?.Invoke(
                        $"1Password session keepalive command failed: {exception.Message}");
                    succeeded = false;
                }
                catch
                {
                    succeeded = false;
                }

                if (succeeded)
                {
                    if (consecutiveFailures > 0)
                    {
                        _log?.Invoke("1Password session keepalive recovered");
                    }
                    else
                    {
                        _log?.Invoke("1Password session keepalive succeeded");
                    }

                    consecutiveFailures = 0;
                    continue;
                }

                consecutiveFailures++;

                if (consecutiveFailures < _maximumConsecutiveFailures)
                {
                    _log?.Invoke(
                        "1Password session keepalive failed; retrying " +
                        $"({consecutiveFailures}/{_maximumConsecutiveFailures})");
                    continue;
                }

                _log?.Invoke("1Password session keepalive could not be renewed");
                _authenticationInvalidated?.Invoke();
                return;
            }
        }
    }
}
