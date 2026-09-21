// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.SshAgent;

namespace OpExec.OnePassword.Tests
{
    internal sealed class FakeOnePasswordCommandRunner : IOnePasswordCommandRunner
    {
        private readonly Queue<OnePasswordCommandResult> _results = new();
        private readonly Queue<OnePasswordSecretCommandResult> _secretResults = new();

        public List<Invocation> Invocations { get; } = new();

        public void EnqueueResult(
            int exitCode,
            string standardOutput,
            string standardError = "")
        {
            _results.Enqueue(
                new OnePasswordCommandResult(exitCode, standardOutput, standardError));
        }

        public void EnqueueSecretResult(int exitCode, byte[] standardOutput)
        {
            _secretResults.Enqueue(
                new OnePasswordSecretCommandResult(
                    exitCode,
                    new SecretBuffer(standardOutput, standardOutput.Length)));
        }

        public Task<OnePasswordCommandResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(
                new Invocation(
                    executablePath,
                    arguments.ToArray(),
                    new Dictionary<string, string?>(environmentVariables)));

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No fake 1Password command result is queued.");
            }

            return Task.FromResult(_results.Dequeue());
        }

        public Task<OnePasswordSecretCommandResult> RunSecretAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(
                new Invocation(
                    executablePath,
                    arguments.ToArray(),
                    new Dictionary<string, string?>(environmentVariables)));

            if (_secretResults.Count == 0)
            {
                throw new InvalidOperationException(
                    "No fake secret 1Password command result is queued.");
            }

            return Task.FromResult(_secretResults.Dequeue());
        }

        public Task<OnePasswordStatusCommandResult> RunStatusAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(
                new Invocation(
                    executablePath,
                    arguments.ToArray(),
                    new Dictionary<string, string?>(environmentVariables)));

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No fake 1Password command result is queued.");
            }

            var result = _results.Dequeue();
            return Task.FromResult(
                new OnePasswordStatusCommandResult(
                    result.ExitCode,
                    result.StandardError));
        }

        internal sealed record Invocation(
            string ExecutablePath,
            IReadOnlyList<string> Arguments,
            IReadOnlyDictionary<string, string?> EnvironmentVariables);
    }
}
