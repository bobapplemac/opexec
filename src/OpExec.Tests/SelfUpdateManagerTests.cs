// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace OpExec.Tests
{
    public sealed class SelfUpdateManagerTests
    {
        [Theory]
        [InlineData("r15", 15)]
        [InlineData("r15-test1", 15)]
        [InlineData("r214", 214)]
        public void ReleaseTagsExposeTheirNumericRevision(
            string tag,
            int expectedRevision)
        {
            Assert.Equal(
                expectedRevision,
                SelfUpdateManager.ParseRevisionTag(tag));
        }

        [Theory]
        [InlineData("15")]
        [InlineData("r")]
        [InlineData("r-1")]
        [InlineData("release15")]
        public void InvalidReleaseTagsAreRejected(string tag)
        {
            Assert.Throws<InvalidOperationException>(
                () => SelfUpdateManager.ParseRevisionTag(tag));
        }

        [Theory]
        [InlineData("r15", "r16", 15, true)]
        [InlineData("r15", "r15", 15, false)]
        [InlineData("r15-test1", "r15", 15, true)]
        [InlineData("r16-test1", "r15", 16, false)]
        public void UpdateAvailabilityUsesRevisionAndStableStatus(
            string currentLabel,
            string latestTag,
            int currentRevision,
            bool expected)
        {
            Assert.Equal(
                expected,
                SelfUpdateManager.IsUpdateAvailable(
                    currentLabel,
                    latestTag,
                    currentRevision));
        }

        [Fact]
        public void ChecksumParserRequiresTheExpectedAssetName()
        {
            const string hash =
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            Assert.Equal(
                hash,
                SelfUpdateManager.ParseChecksum(
                    $"{hash}  opexec-r16-linux-x64.tar.gz\n",
                    "opexec-r16-linux-x64.tar.gz"));
            Assert.Throws<InvalidDataException>(
                () => SelfUpdateManager.ParseChecksum(
                    $"{hash}  different.tar.gz\n",
                    "opexec-r16-linux-x64.tar.gz"));
        }

        [Fact]
        public void ReleaseArchiveMustContainOnlyTheExecutable()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"opexec-update-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                var archivePath = Path.Combine(directory, "release.tar.gz");
                WriteArchive(archivePath, ("opexec", "new-binary"));

                var executablePath = SelfUpdateManager.ExtractExecutable(
                    archivePath,
                    directory);

                Assert.Equal("new-binary", File.ReadAllText(executablePath));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void ReleaseArchiveRejectsAdditionalEntries()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"opexec-update-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                var archivePath = Path.Combine(directory, "release.tar.gz");
                WriteArchive(
                    archivePath,
                    ("opexec", "new-binary"),
                    ("unexpected", "extra"));

                Assert.Throws<InvalidDataException>(
                    () => SelfUpdateManager.ExtractExecutable(
                        archivePath,
                        directory));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static void WriteArchive(
            string path,
            params (string Name, string Content)[] files)
        {
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionMode.Compress);
            using var writer = new TarWriter(gzip);

            foreach (var (name, content) in files)
            {
                var entry = new UstarTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
                };
                writer.WriteEntry(entry);
            }
        }
    }
}
