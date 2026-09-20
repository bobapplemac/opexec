// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class ForegroundProcessRequest
    {
        public ForegroundProcessRequest(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string?>? environmentVariables = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

            ExecutablePath = executablePath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            EnvironmentVariables = new Dictionary<string, string?>(
                environmentVariables ?? new Dictionary<string, string?>(),
                StringComparer.Ordinal);
        }

        public string ExecutablePath { get; }

        public IReadOnlyList<string> Arguments { get; }

        public string WorkingDirectory { get; }

        public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }

        public ForegroundProcessRequest WithEnvironmentVariable(
            string name,
            string? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var environmentVariables = new Dictionary<string, string?>(
                EnvironmentVariables,
                StringComparer.Ordinal)
            {
                [name] = value
            };

            return new ForegroundProcessRequest(
                ExecutablePath,
                Arguments,
                WorkingDirectory,
                environmentVariables);
        }

        public ForegroundProcessRequest WithEnvironmentVariables(
            IReadOnlyDictionary<string, string?> variables)
        {
            ArgumentNullException.ThrowIfNull(variables);

            var environmentVariables = new Dictionary<string, string?>(
                EnvironmentVariables,
                StringComparer.Ordinal);

            foreach (var variable in variables)
            {
                environmentVariables[variable.Key] = variable.Value;
            }

            return new ForegroundProcessRequest(
                ExecutablePath,
                Arguments,
                WorkingDirectory,
                environmentVariables);
        }
    }
}
