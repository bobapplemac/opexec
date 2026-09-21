// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpExec
{
    internal sealed class SelfUpdateManager
    {
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/bobapplemac/opexec/releases/latest";
        private const long MaximumAssetSize = 512L * 1024 * 1024;
        private static readonly HttpClient Client = CreateHttpClient();

        public async Task<int> RunAsync(
            InvocationRequest invocation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            if (!OperatingSystem.IsLinux() ||
                RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                throw new PlatformNotSupportedException(
                    "Automatic updates are supported only on Linux x64.");
            }

            var destinationDirectory =
                SelfInstallationManager.ResolveDestinationDirectory(
                    InvocationAction.Update,
                    invocation.UserInstallation);
            var currentLabel = ProductVersion.GetRevisionLabel(
                typeof(SelfUpdateManager).Assembly);
            var currentRevision = ParseRevisionTag(currentLabel);

            Console.Out.WriteLine("Checking GitHub for the latest stable OpExec release...");
            var release = await GetLatestReleaseAsync(cancellationToken);

            if (!IsUpdateAvailable(currentLabel, release.Tag, currentRevision))
            {
                Console.Out.WriteLine(
                    $"OpExec {currentLabel} is already up to date (latest: {release.Tag}).");
                return 0;
            }

            if (!invocation.AssumeYes && !ConfirmUpdate(currentLabel, release.Tag))
            {
                Console.Out.WriteLine("Update cancelled.");
                return 0;
            }

            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"opexec-update-{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                File.SetUnixFileMode(
                    temporaryDirectory,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute);
                var archivePath = Path.Combine(
                    temporaryDirectory,
                    release.ArchiveName);
                var checksumPath = Path.Combine(
                    temporaryDirectory,
                    release.ChecksumName);

                Console.Out.WriteLine($"Downloading OpExec {release.Tag}...");
                await DownloadAsync(
                    release.ArchiveUrl,
                    archivePath,
                    MaximumAssetSize,
                    cancellationToken);
                await DownloadAsync(
                    release.ChecksumUrl,
                    checksumPath,
                    4096,
                    cancellationToken);

                Console.Out.WriteLine($"Verifying {release.ArchiveName}...");
                await VerifyChecksumAsync(
                    archivePath,
                    checksumPath,
                    release.ArchiveName,
                    cancellationToken);
                var executablePath = ExtractExecutable(
                    archivePath,
                    temporaryDirectory);

                SelfInstallationManager.Install(
                    executablePath,
                    destinationDirectory,
                    force: false);
                Console.Out.WriteLine(
                    $"Updated opexec, opshell, and opssh to {release.Tag} in {destinationDirectory}.");
                return 0;
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        internal static int ParseRevisionTag(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var separator = value.IndexOf('-');
            var tag = separator < 0 ? value : value[..separator];

            if (tag.Length < 2 || tag[0] != 'r' ||
                !int.TryParse(tag.AsSpan(1), out var revision) ||
                revision < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid OpExec release tag: {value}");
            }

            return revision;
        }

        internal static bool IsUpdateAvailable(
            string currentLabel,
            string latestTag,
            int currentRevision)
        {
            var latestRevision = ParseRevisionTag(latestTag);
            return latestRevision > currentRevision ||
                (latestRevision == currentRevision &&
                    !string.Equals(
                        currentLabel,
                        latestTag,
                        StringComparison.Ordinal));
        }

        internal static string ParseChecksum(
            string checksumText,
            string expectedFileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(checksumText);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedFileName);
            var fields = checksumText.Trim().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length != 2 ||
                fields[0].Length != 64 ||
                !fields[0].All(Uri.IsHexDigit) ||
                fields[1].TrimStart('*') != expectedFileName)
            {
                throw new InvalidDataException(
                    $"The checksum for {expectedFileName} has an invalid format.");
            }

            return fields[0];
        }

        private static bool ConfirmUpdate(string currentLabel, string latestTag)
        {
            Console.Out.Write(
                $"Update OpExec from {currentLabel} to {latestTag}? [y/N] ");
            var answer = Console.In.ReadLine();
            return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<Release> GetLatestReleaseAsync(
            CancellationToken cancellationToken)
        {
            using var response = await Client.GetAsync(
                LatestReleaseUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString()
                ?? throw new InvalidDataException(
                    "GitHub returned a release without a tag.");
            var revision = ParseRevisionTag(tag);

            if (root.GetProperty("draft").GetBoolean() ||
                root.GetProperty("prerelease").GetBoolean() ||
                tag != $"r{revision}")
            {
                throw new InvalidDataException(
                    "GitHub returned a draft or prerelease as the latest stable release.");
            }

            var archiveName = $"opexec-{tag}-linux-x64.tar.gz";
            var checksumName = $"{archiveName}.sha256";
            Uri? archiveUrl = null;
            Uri? checksumUrl = null;

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var urlValue = asset.GetProperty("browser_download_url").GetString();

                if (!Uri.TryCreate(urlValue, UriKind.Absolute, out var url) ||
                    url.Scheme != Uri.UriSchemeHttps)
                {
                    continue;
                }

                if (name == archiveName)
                {
                    archiveUrl = url;
                }
                else if (name == checksumName)
                {
                    checksumUrl = url;
                }
            }

            return new Release(
                tag,
                archiveName,
                checksumName,
                archiveUrl ?? throw new InvalidDataException(
                    $"GitHub release {tag} does not contain {archiveName}."),
                checksumUrl ?? throw new InvalidDataException(
                    $"GitHub release {tag} does not contain {checksumName}."));
        }

        private static async Task DownloadAsync(
            Uri url,
            string destinationPath,
            long maximumSize,
            CancellationToken cancellationToken)
        {
            using var response = await Client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength > maximumSize)
            {
                throw new InvalidDataException(
                    $"The download exceeds the {maximumSize}-byte size limit.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            long total = 0;

            while (true)
            {
                var count = await source.ReadAsync(buffer, cancellationToken);

                if (count == 0)
                {
                    break;
                }

                total += count;

                if (total > maximumSize)
                {
                    throw new InvalidDataException(
                        $"The download exceeds the {maximumSize}-byte size limit.");
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, count),
                    cancellationToken);
            }
        }

        private static async Task VerifyChecksumAsync(
            string archivePath,
            string checksumPath,
            string archiveName,
            CancellationToken cancellationToken)
        {
            var checksumText = await File.ReadAllTextAsync(
                checksumPath,
                cancellationToken);
            var expected = Convert.FromHexString(
                ParseChecksum(checksumText, archiveName));
            await using var archive = File.OpenRead(archivePath);
            var actual = await SHA256.HashDataAsync(archive, cancellationToken);

            try
            {
                if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                {
                    throw new InvalidDataException(
                        $"The SHA-256 checksum for {archiveName} does not match.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(actual);
            }
        }

        internal static string ExtractExecutable(
            string archivePath,
            string temporaryDirectory)
        {
            var executablePath = Path.Combine(temporaryDirectory, "opexec");
            using var archive = File.OpenRead(archivePath);
            using var gzip = new GZipStream(archive, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            var entry = reader.GetNextEntry(copyData: false);

            if (entry is null ||
                entry.Name != "opexec" ||
                entry.EntryType is not (TarEntryType.RegularFile or
                    TarEntryType.V7RegularFile) ||
                entry.Length > MaximumAssetSize ||
                entry.DataStream is null)
            {
                throw new InvalidDataException(
                    "The release archive does not contain exactly one opexec executable.");
            }

            using var destination = new FileStream(
                executablePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            entry.DataStream.CopyTo(destination);

            if (reader.GetNextEntry(copyData: false) is not null)
            {
                throw new InvalidDataException(
                    "The release archive does not contain exactly one opexec executable.");
            }

            return executablePath;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("opexec", "1"));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        private sealed record Release(
            string Tag,
            string ArchiveName,
            string ChecksumName,
            Uri ArchiveUrl,
            Uri ChecksumUrl);
    }
}
