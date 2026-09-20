// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    public sealed class SshAgentOptions
    {
        public const int DefaultMaximumPacketLength = 256 * 1024;

        public string RuntimeDirectoryName { get; init; } = "opssh";

        public string? RuntimeBaseDirectory { get; init; }

        public int MaximumPacketLength { get; init; } = DefaultMaximumPacketLength;

        public ISshIdentityProvider? IdentityProvider { get; init; }

        public Action<string>? Log { get; init; }
    }
}
