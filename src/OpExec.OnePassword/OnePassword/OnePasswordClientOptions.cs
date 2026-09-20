// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public sealed class OnePasswordClientOptions
    {
        public string ExecutablePath { get; init; } = "op";

        public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
            new Dictionary<string, string?>();
    }
}
