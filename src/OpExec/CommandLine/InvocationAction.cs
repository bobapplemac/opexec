// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal enum InvocationAction
    {
        ExecuteCommand,
        StartShell,
        StartAgent,
        StopAgent,
        StopAllAgents,
        Install,
        Update,
        Uninstall,
        ShowHelp,
        ShowAgentHelp,
        ShowVersion,
        ShowLicenses,
        ShowUsage
    }
}
