// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class ShellResolution
    {
        public ShellResolution(string executablePath, IReadOnlyList<string> arguments)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentNullException.ThrowIfNull(arguments);

            ExecutablePath = executablePath;
            Arguments = arguments;
        }

        public string ExecutablePath { get; }

        public IReadOnlyList<string> Arguments { get; }
    }
}
