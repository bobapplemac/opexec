// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed record AgentDaemonStartResult(
        int ProcessId,
        string SocketPath,
        string LogPath,
        bool Verbose);
}
