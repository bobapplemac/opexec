// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class InvocationRequest
    {
        public InvocationRequest(
            InvocationMode mode,
            InvocationAction action,
            string? command,
            IReadOnlyList<string>? arguments,
            bool loginShell,
            bool verbose,
            bool quiet,
            bool daemon = false,
            bool userInstallation = false,
            bool force = false,
            bool assumeYes = false)
        {
            Mode = mode;
            Action = action;
            Command = command;
            Arguments = arguments ?? Array.Empty<string>();
            LoginShell = loginShell;
            Verbose = verbose;
            Quiet = quiet;
            Daemon = daemon;
            UserInstallation = userInstallation;
            Force = force;
            AssumeYes = assumeYes;
        }

        public InvocationMode Mode { get; }

        public InvocationAction Action { get; }

        public string? Command { get; }

        public IReadOnlyList<string> Arguments { get; }

        public bool LoginShell { get; }

        public bool Verbose { get; }

        public bool Quiet { get; }

        public bool Daemon { get; }

        public bool UserInstallation { get; }

        public bool Force { get; }

        public bool AssumeYes { get; }
    }
}
