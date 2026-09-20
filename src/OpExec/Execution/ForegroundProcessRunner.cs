// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        ForegroundProcessRunner.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Runs the user-selected foreground process with inherited terminal handles,
//              argument-list isolation, unchanged exit-code propagation, and cancellation-driven
//              process-tree termination.
// ------------------------------------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;

namespace OpExec
{
    internal sealed class ForegroundProcessRunner : IForegroundProcessRunner
    {
        public async Task<int> RunAsync(
            ForegroundProcessRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Deliberate exception to the project's CliWrap rule: CliWrap always redirects
            // standard streams through pipes. A foreground ssh client, interactive shell,
            // or TUI must inherit the actual terminal handles for TTY detection, terminal
            // modes, job control, and window sizing. Keep this unredirected boundary small.
            var startInfo = new ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            foreach (var argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var variable in request.EnvironmentVariables)
            {
                if (variable.Value is null)
                {
                    startInfo.Environment.Remove(variable.Key);
                }
                else
                {
                    startInfo.Environment[variable.Key] = variable.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        $"Unable to start target executable '{request.ExecutablePath}'.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    $"Unable to start target executable '{request.ExecutablePath}': " +
                    exception.Message,
                    exception);
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken);
                return process.ExitCode;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
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
                // The process exited between the state check and the kill request.
            }
            catch (Win32Exception)
            {
                // Preserve the original cancellation outcome.
            }
        }
    }
}
