// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    internal static class AgentMessageType
    {
        public const byte Failure = 5;
        public const byte RequestIdentities = 11;
        public const byte IdentitiesAnswer = 12;
        public const byte SignRequest = 13;
        public const byte SignResponse = 14;
    }
}
