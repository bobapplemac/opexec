// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Diagnostics;

namespace OpExec
{
    internal sealed class AgentDaemonLauncher
    {
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

        public async Task<AgentDaemonStartResult> StartAsync(
            IReadOnlyDictionary<string, string?> environmentVariables,
            bool verbose,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(environmentVariables);

            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Detached opssh agent mode is currently supported only on Linux.");
            }

            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "The current opexec executable path could not be determined.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var entryPath = Environment.GetCommandLineArgs().FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(entryPath) &&
                entryPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(entryPath);
            }

            startInfo.ArgumentList.Add(AgentDaemonWorker.InternalArgument);

            if (verbose)
            {
                startInfo.ArgumentList.Add("--verbose");
            }

            foreach (var variable in environmentVariables)
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
                        "Unable to start the detached opssh agent process.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    "The detached opssh agent process could not be started.",
                    exception);
            }

            process.StandardInput.Close();

            try
            {
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                startup.CancelAfter(StartupTimeout);
                var response = await process.StandardOutput.ReadLineAsync(startup.Token);

                if (response is not null &&
                    AgentDaemonProtocol.TryParseReady(
                        response,
                        out var socketPath,
                        out var logPath))
                {
                    return new AgentDaemonStartResult(
                        process.Id,
                        socketPath,
                        logPath,
                        verbose);
                }

                if (response is not null &&
                    AgentDaemonProtocol.TryParseError(response, out var error))
                {
                    await process.WaitForExitAsync(CancellationToken.None);
                    throw new InvalidOperationException(error);
                }

                if (response is not null)
                {
                    throw new InvalidDataException(
                        "The detached opssh agent returned an invalid startup response.");
                }

                var standardError = await process.StandardError.ReadToEndAsync(
                    CancellationToken.None);
                await process.WaitForExitAsync(CancellationToken.None);
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(standardError)
                        ? "The detached opssh agent exited before reporting readiness."
                        : standardError.Trim());
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw new TimeoutException(
                    "The detached opssh agent did not report readiness within 30 seconds.");
            }
            catch
            {
                TryKill(process);
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
                    process.WaitForExit();
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
