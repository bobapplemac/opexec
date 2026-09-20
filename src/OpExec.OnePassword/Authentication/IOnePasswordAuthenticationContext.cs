// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public interface IOnePasswordAuthenticationContext : IDisposable
    {
        IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }
    }
}
