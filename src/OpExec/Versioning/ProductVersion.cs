// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Reflection;

namespace OpExec
{
    internal static class ProductVersion
    {
        public static string GetDisplayVersion()
        {
            return $"opexec {GetRevisionLabel(typeof(ProductVersion).Assembly)}";
        }

        internal static string GetRevisionLabel(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            var fileVersionValue = assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version
                ?? throw new InvalidOperationException(
                    $"{assembly.GetName().Name} has no file version metadata.");

            if (!Version.TryParse(fileVersionValue, out var version) ||
                version.Minor < 0)
            {
                throw new InvalidOperationException(
                    $"{assembly.GetName().Name} has invalid file version metadata: {fileVersionValue}");
            }

            var canonicalRevision = $"r{version.Minor}";
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? throw new InvalidOperationException(
                    $"{assembly.GetName().Name} has no informational version metadata.");

            if (string.Equals(
                    informationalVersion,
                    canonicalRevision,
                    StringComparison.Ordinal) ||
                informationalVersion.StartsWith(
                    $"{canonicalRevision}-",
                    StringComparison.Ordinal))
            {
                return informationalVersion;
            }

            throw new InvalidOperationException(
                $"{assembly.GetName().Name} has inconsistent version metadata: " +
                $"file version {fileVersionValue}, informational version {informationalVersion}.");
        }
    }
}
