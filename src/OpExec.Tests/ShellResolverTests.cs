// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.Tests
{
    public sealed class ShellResolverTests
    {
        [Fact]
        public void EnvironmentShellTakesPrecedenceWhenExecutable()
        {
            var result = ShellResolver.ResolvePath(
                "/custom/bash",
                "alice",
                "alice:x:1:1::/home/alice:/bin/zsh",
                path => path is "/custom/bash" or "/bin/zsh" or "/bin/sh");

            Assert.Equal("/custom/bash", result);
        }

        [Fact]
        public void PasswdShellIsUsedWhenEnvironmentShellIsUnavailable()
        {
            var result = ShellResolver.ResolvePath(
                "/missing/bash",
                "alice",
                "root:x:0:0::/root:/bin/bash\nalice:x:1:1::/home/alice:/bin/zsh\n",
                path => path is "/bin/zsh" or "/bin/sh");

            Assert.Equal("/bin/zsh", result);
        }

        [Fact]
        public void BinShIsFinalFallback()
        {
            var result = ShellResolver.ResolvePath(
                null,
                "alice",
                null,
                path => path == "/bin/sh");

            Assert.Equal("/bin/sh", result);
        }

        [Fact]
        public void InteractiveShellUsesInteractiveFlag()
        {
            Assert.Equal(
                new[] { "-i" },
                ShellResolver.CreateArguments("/bin/bash", loginShell: false));
        }

        [Theory]
        [InlineData("/bin/bash")]
        [InlineData("/usr/bin/zsh")]
        [InlineData("/bin/fish")]
        public void SupportedLoginShellUsesLoginFlag(string path)
        {
            Assert.Equal(
                new[] { "-l" },
                ShellResolver.CreateArguments(path, loginShell: true));
        }

        [Fact]
        public void UnknownLoginShellFailsClearly()
        {
            var exception = Assert.Throws<NotSupportedException>(
                () => ShellResolver.CreateArguments("/opt/custom-shell", loginShell: true));

            Assert.Contains("not defined", exception.Message);
        }
    }
}
