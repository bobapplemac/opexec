// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.Tests
{
    public sealed class LicenseNoticeWriterTests
    {
        [Fact]
        public void WriteIncludesEveryBundledLicenseAndNoticeSet()
        {
            using var writer = new StringWriter();

            LicenseNoticeWriter.Write(writer);

            var output = writer.ToString();
            Assert.Contains("===== OpExec =====", output);
            Assert.Contains("===== Mono.Options 6.12.0.148 =====", output);
            Assert.Contains("===== CliWrap 3.10.5 =====", output);
            Assert.Contains("===== BouncyCastle.Cryptography 2.7.0 =====", output);
            Assert.Contains("===== .NET Runtime 10.0.12 =====", output);
            Assert.Contains(
                "===== .NET Runtime 10.0.12 Third-Party Notices =====",
                output);
            Assert.Contains("Copyright (C) 2008 Novell", output);
            Assert.Contains("Copyright (c) 2017-2026 Oleksii Holub", output);
            Assert.Contains(
                "Copyright (c) 2000-2026 The Legion of the Bouncy Castle Inc.",
                output);
            Assert.True(output.Length > 75_000);
        }
    }
}
