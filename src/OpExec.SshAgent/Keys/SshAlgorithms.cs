// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    internal static class SshAlgorithms
    {
        public const string Ed25519 = "ssh-ed25519";
        public const string Rsa = "ssh-rsa";
        public const string RsaSha2_256 = "rsa-sha2-256";
        public const string RsaSha2_512 = "rsa-sha2-512";
    }
}
