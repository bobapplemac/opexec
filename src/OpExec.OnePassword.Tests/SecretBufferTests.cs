// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.SshAgent;
using Xunit;

namespace OpExec.OnePassword.Tests
{
    public sealed class SecretBufferTests
    {
        [Fact]
        public void DisposeZeroesEntireOwnedArray()
        {
            var bytes = Enumerable.Repeat((byte)42, 32).ToArray();
            var buffer = new SecretBuffer(bytes, 16);

            buffer.Dispose();

            Assert.All(bytes, value => Assert.Equal(0, value));
            Assert.Throws<ObjectDisposedException>(() => ReadBuffer(buffer));
        }

        [Fact]
        public void StreamDetachTransfersBytesToZeroableBuffer()
        {
            var source = Enumerable.Repeat((byte)7, 5000).ToArray();
            using var stream = new ZeroingBufferStream();
            stream.Write(source);

            using var buffer = stream.Detach();

            Assert.Equal(source, buffer.Span.ToArray());
        }

        [Fact]
        public async Task CommandRunnerCapturesSecretOutputWithoutClosingPipe()
        {
            Assert.SkipUnless(
                OperatingSystem.IsWindows(),
                "The anonymous-pipe regression is specific to Windows.");

            var runner = new OnePasswordCommandRunner();
            var result = await runner.RunSecretAsync(
                "powershell.exe",
                new[]
                {
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    "[Console]::Out.Write('secret-output')"
                },
                new Dictionary<string, string?>(),
                CancellationToken.None);

            using var output = result.StandardOutput;
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("secret-output"u8.ToArray(), output.Span.ToArray());
        }

        [Fact]
        public void SecretOutputBufferEnforcesConfiguredLimit()
        {
            using var stream = new ZeroingBufferStream(4);
            stream.Write(new byte[] { 1, 2, 3, 4 });

            var exception = Assert.Throws<InvalidDataException>(
                () => stream.WriteByte(5));

            Assert.Contains("safety limit", exception.Message);
        }

        private static byte[] ReadBuffer(SecretBuffer buffer)
        {
            return buffer.Span.ToArray();
        }
    }
}
