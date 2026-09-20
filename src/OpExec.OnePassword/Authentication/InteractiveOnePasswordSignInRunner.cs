// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Diagnostics;

namespace OpExec.OnePassword
{
    public sealed class InteractiveOnePasswordSignInRunner : IInteractiveOnePasswordSignInRunner
    {
        internal const int MaximumSessionOutputLength = 16 * 1024;

        public async Task<InteractiveSignInResult> RunAsync(
            CancellationToken cancellationToken)
        {
            // This interactive helper must inherit the real terminal for credential and
            // MFA prompts while capturing only --raw's secret stdout. CliWrap cannot mix
            // inherited and redirected standard handles, so this is a documented exception.
            var startInfo = new ProcessStartInfo
            {
                FileName = "op",
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false
            };
            startInfo.ArgumentList.Add("signin");
            startInfo.ArgumentList.Add("--raw");

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Unable to start the 1Password CLI for interactive sign-in.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    "The 1Password CLI executable 'op' could not be started. " +
                    "Install it or ensure it is available on PATH.",
                    exception);
            }

            try
            {
                var outputTask = ReadBoundedOutputAsync(
                    process.StandardOutput,
                    cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                return new InteractiveSignInResult(
                    process.ExitCode,
                    await outputTask);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }
        }

        internal static async Task<string> ReadBoundedOutputAsync(
            TextReader reader,
            CancellationToken cancellationToken)
        {
            var captured = new char[MaximumSessionOutputLength];
            var overflow = new char[256];
            var capturedLength = 0;

            try
            {
                while (capturedLength < captured.Length)
                {
                    var read = await reader.ReadAsync(
                        captured.AsMemory(capturedLength),
                        cancellationToken);

                    if (read == 0)
                    {
                        return new string(captured, 0, capturedLength);
                    }

                    capturedLength += read;
                }

                if (await reader.ReadAsync(overflow, cancellationToken) == 0)
                {
                    return new string(captured, 0, capturedLength);
                }

                throw new InvalidDataException(
                    "The 1Password CLI sign-in returned unexpectedly large output.");
            }
            finally
            {
                Array.Clear(captured);
                Array.Clear(overflow);
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }
        }
    }
}
