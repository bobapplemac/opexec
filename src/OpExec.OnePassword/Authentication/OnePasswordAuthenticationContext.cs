// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public sealed class OnePasswordAuthenticationContext : IOnePasswordAuthenticationContext
    {
        private readonly Dictionary<string, string?> _environmentVariables;
        private bool _disposed;

        public OnePasswordAuthenticationContext(
            IReadOnlyDictionary<string, string?>? environmentVariables = null)
        {
            _environmentVariables = new Dictionary<string, string?>(
                environmentVariables ?? new Dictionary<string, string?>(),
                StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, string?> EnvironmentVariables
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _environmentVariables;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _environmentVariables.Clear();
        }
    }
}
