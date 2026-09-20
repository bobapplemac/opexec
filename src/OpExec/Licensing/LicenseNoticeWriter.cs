// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        LicenseNoticeWriter.cs
// Revision:    r5
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Streams the OpExec and bundled dependency license resources to a caller-
//              supplied writer in a stable order without retaining the complete notice set.
// ------------------------------------------------------------------------------------------

using System.Reflection;

namespace OpExec
{
    internal static class LicenseNoticeWriter
    {
        private const int CopyBufferSize = 4096;

        private static readonly LicenseResource[] Resources =
        {
            new("OpExec", "OpExec.Licenses.OpExec.txt"),
            new(
                "Mono.Options 6.12.0.148",
                "OpExec.Licenses.Mono.Options-6.12.0.148.txt"),
            new("CliWrap 3.10.5", "OpExec.Licenses.CliWrap-3.10.5.txt"),
            new(
                "BouncyCastle.Cryptography 2.7.0",
                "OpExec.Licenses.BouncyCastle.Cryptography-2.7.0.txt"),
            new(
                ".NET Runtime 10.0.12",
                "OpExec.Licenses.dotnet-runtime-10.0.12.txt"),
            new(
                ".NET Runtime 10.0.12 Third-Party Notices",
                "OpExec.Licenses.dotnet-runtime-10.0.12-THIRD-PARTY-NOTICES.txt")
        };

        public static void Write(TextWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            var assembly = typeof(LicenseNoticeWriter).Assembly;
            var buffer = new char[CopyBufferSize];

            for (var index = 0; index < Resources.Length; index++)
            {
                var resource = Resources[index];

                if (index > 0)
                {
                    writer.WriteLine();
                }

                writer.WriteLine($"===== {resource.DisplayName} =====");
                writer.WriteLine();

                using var stream = assembly.GetManifestResourceStream(resource.Name)
                    ?? throw new InvalidOperationException(
                        $"Embedded license resource is missing: {resource.Name}");
                using var reader = new StreamReader(stream);

                int charactersRead;
                while ((charactersRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, charactersRead);
                }

                writer.WriteLine();
            }
        }

        private sealed record LicenseResource(string DisplayName, string Name);
    }
}
