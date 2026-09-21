// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        LicenseNoticeWriter.cs
// Revision:    r6
// Modified:    2026-09-20
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
                "Third-Party Notices",
                "OpExec.Licenses.ThirdPartyNotices.txt")
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
