// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        OnePasswordCommandRunner.cs
// Revision:    r9
// Modified:    2026-09-20
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Runs short-lived 1Password CLI helper commands through CliWrap with explicit
//              environment control, bounded or discarded output, cancellation, and secret-safe
//              failure behavior.
// ------------------------------------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using CliWrap;
using CliWrap.Buffered;
using OpExec.SshAgent;

namespace OpExec.OnePassword
{
    internal interface IOnePasswordCommandRunner
    {
        Task<OnePasswordCommandResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken);

        Task<OnePasswordSecretCommandResult> RunSecretAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken);

        Task<OnePasswordStatusCommandResult> RunStatusAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken);

    }

    internal sealed record OnePasswordCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    internal sealed record OnePasswordSecretCommandResult(
        int ExitCode,
        SecretBuffer StandardOutput);

    internal sealed record OnePasswordStatusCommandResult(
        int ExitCode,
        string StandardError);

    internal sealed class OnePasswordCommandRunner : IOnePasswordCommandRunner
    {
        internal const int MaximumSecretOutputLength = 1024 * 1024;
        internal const int MaximumDiagnosticOutputLength = 64 * 1024;

        public async Task<OnePasswordCommandResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            try
            {
                // CliWrap's PipeTarget.Null leaves the OS handle unopened. Use an
                // explicit null stream so detached workers do not pass their closed
                // inherited stderr handle to the 1Password CLI.
                var command = Cli.Wrap(executablePath)
                    .WithArguments(arguments)
                    .WithEnvironmentVariables(environmentVariables)
                    .WithValidation(CommandResultValidation.None);
                var result = await command.ExecuteBufferedAsync(cancellationToken);

                return new OnePasswordCommandResult(
                    result.ExitCode,
                    result.StandardOutput,
                    result.StandardError);
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    $"The 1Password CLI executable '{executablePath}' could not be started. " +
                    "Install it or correct the configured executable path.",
                    exception);
            }
        }

        public async Task<OnePasswordSecretCommandResult> RunSecretAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            using var standardOutput = new ZeroingBufferStream(MaximumSecretOutputLength);

            try
            {
                var command = Cli.Wrap(executablePath)
                    .WithArguments(arguments)
                    .WithEnvironmentVariables(environmentVariables)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(standardOutput))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Stream.Null));
                var result = await command.ExecuteAsync(cancellationToken);

                return new OnePasswordSecretCommandResult(
                    result.ExitCode,
                    standardOutput.Detach());
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    $"The 1Password CLI executable '{executablePath}' could not be started. " +
                    "Install it or correct the configured executable path.",
                    exception);
            }
        }

        public async Task<OnePasswordStatusCommandResult> RunStatusAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string?> environmentVariables,
            CancellationToken cancellationToken)
        {
            using var standardError = new ZeroingBufferStream(
                MaximumDiagnosticOutputLength);

            try
            {
                var command = Cli.Wrap(executablePath)
                    .WithArguments(arguments)
                    .WithEnvironmentVariables(environmentVariables)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Stream.Null))
                    .WithStandardErrorPipe(PipeTarget.ToStream(standardError));
                var result = await command.ExecuteAsync(cancellationToken);
                using var capturedError = standardError.Detach();

                return new OnePasswordStatusCommandResult(
                    result.ExitCode,
                    Encoding.UTF8.GetString(capturedError.Span));
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    $"The 1Password CLI executable '{executablePath}' could not be started. " +
                    "Install it or correct the configured executable path.",
                    exception);
            }
        }

    }
}
