// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        CommandLineParser.cs
// Revision:    r13
// Modified:    2026-09-21
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Mono.Options-based parser for the opexec, opshell, and opssh interfaces,
//              including wrapper boundaries, agent lifecycle commands, installation actions,
//              help and license actions, validation, and alias-specific behavior.
// ------------------------------------------------------------------------------------------

using Mono.Options;

namespace OpExec
{
    internal sealed class CommandLineParser
    {
        public CommandLineParseResult Parse(
            string invocationPath,
            IReadOnlyList<string> arguments)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(invocationPath);
            ArgumentNullException.ThrowIfNull(arguments);

            var mode = ResolveMode(invocationPath);

            return mode switch
            {
                InvocationMode.OpShell => ParseOpShell(arguments),
                InvocationMode.OpSsh => ParseOpSsh(arguments),
                _ => ParseOpExec(arguments)
            };
        }

        public void WriteUsage(TextWriter writer, InvocationMode mode)
        {
            ArgumentNullException.ThrowIfNull(writer);

            switch (mode)
            {
                case InvocationMode.OpShell:
                    writer.WriteLine("Usage: opshell [OPTIONS]");
                    writer.WriteLine();
                    writer.WriteLine("Start an interactive shell in the scoped execution context.");
                    writer.WriteLine();
                    writer.WriteLine("Options:");
                    CreateOptionSet(
                            new OptionValues(),
                            includeInteractive: false,
                            includeLogin: true,
                            includeLifecycle: false)
                        .WriteOptionDescriptions(writer);
                    break;
                case InvocationMode.OpSsh:
                    writer.WriteLine("Usage: opssh [SSH_ARGUMENTS...]");
                    writer.WriteLine("       opssh --agent [OPTIONS]");
                    writer.WriteLine("       opssh {--help|--version|--licenses}");
                    writer.WriteLine();
                    writer.WriteLine("Run ssh with arguments passed through unchanged.");
                    writer.WriteLine();
                    writer.WriteLine("Wrapper-only standalone options:");
                    writer.WriteLine("  --help                  Show this help and exit.");
                    writer.WriteLine("  --version               Show version and exit.");
                    writer.WriteLine("  --licenses              Show bundled license notices and exit.");
                    writer.WriteLine();
                    writer.WriteLine(
                        "All other arguments, including SSH short options, are passed to ssh unchanged.");
                    break;
                default:
                    writer.WriteLine("Usage: opexec [OPTIONS] COMMAND [ARGUMENTS...]");
                    writer.WriteLine("       opexec -i");
                    writer.WriteLine("       opexec --install [--user] [--force]");
                    writer.WriteLine("       opexec --update [--user] [-y|--yes]");
                    writer.WriteLine("       opexec --uninstall [--user]");
                    writer.WriteLine();
                    writer.WriteLine("Run a command in the scoped execution context.");
                    writer.WriteLine();
                    writer.WriteLine("Options:");
                    CreateOptionSet(
                            new OptionValues(),
                            includeInteractive: true,
                            includeLogin: false,
                            includeLifecycle: true)
                        .WriteOptionDescriptions(writer);
                    writer.WriteLine();
                    writer.WriteLine("Wrapper options are parsed only before COMMAND.");
                    break;
            }
        }

        public void WriteAgentUsage(TextWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteLine("Usage: opssh --agent [OPTIONS]");
            writer.WriteLine();
            writer.WriteLine("Run the 1Password-backed SSH agent.");
            writer.WriteLine();
            writer.WriteLine("Options:");
            CreateAgentOptionSet(new AgentOptionValues())
                .WriteOptionDescriptions(writer);
        }

        public static InvocationMode ResolveMode(string invocationPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(invocationPath);

            var separatorIndex = Math.Max(
                invocationPath.LastIndexOf('/'),
                invocationPath.LastIndexOf('\\'));
            var fileName = invocationPath[(separatorIndex + 1)..];

            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^4];
            }

            return fileName switch
            {
                "opshell" => InvocationMode.OpShell,
                "opssh" => InvocationMode.OpSsh,
                _ => InvocationMode.OpExec
            };
        }

        public static string GetVersion()
        {
            return ProductVersion.GetDisplayVersion();
        }

        private static CommandLineParseResult ParseOpExec(IReadOnlyList<string> arguments)
        {
            var values = new OptionValues();
            var options = CreateOptionSet(
                values,
                includeInteractive: true,
                includeLogin: false,
                includeLifecycle: true);
            var optionArguments = new List<string>();
            var childIndex = FindChildIndex(arguments, optionArguments);

            var error = ParseOptions(options, optionArguments);

            if (error is not null)
            {
                return CommandLineParseResult.Failure(error);
            }

            error = ValidateCommonOptions(values);

            if (error is not null)
            {
                return CommandLineParseResult.Failure(error);
            }

            var hasChild = childIndex < arguments.Count;

            if (values.ShowHelp)
            {
                return Success(InvocationMode.OpExec, InvocationAction.ShowHelp, values);
            }

            if (values.ShowVersion)
            {
                return Success(InvocationMode.OpExec, InvocationAction.ShowVersion, values);
            }

            if (values.ShowLicenses)
            {
                return Success(InvocationMode.OpExec, InvocationAction.ShowLicenses, values);
            }

            error = ValidateLifecycleOptions(values, hasChild);

            if (error is not null)
            {
                return CommandLineParseResult.Failure(error);
            }

            if (values.Install)
            {
                return LifecycleSuccess(InvocationAction.Install, values);
            }

            if (values.Update)
            {
                return LifecycleSuccess(InvocationAction.Update, values);
            }

            if (values.Uninstall)
            {
                return LifecycleSuccess(InvocationAction.Uninstall, values);
            }

            if (values.Interactive)
            {
                if (hasChild)
                {
                    return CommandLineParseResult.Failure(
                        "The -i option cannot be combined with a child command.");
                }

                return Success(InvocationMode.OpExec, InvocationAction.StartShell, values);
            }

            if (!hasChild)
            {
                return Success(InvocationMode.OpExec, InvocationAction.ShowUsage, values);
            }

            var childArguments = arguments.Skip(childIndex + 1).ToArray();
            return CommandLineParseResult.Success(
                new InvocationRequest(
                    InvocationMode.OpExec,
                    InvocationAction.ExecuteCommand,
                    arguments[childIndex],
                    childArguments,
                    loginShell: false,
                    values.Verbose,
                    values.Quiet));
        }

        private static CommandLineParseResult ParseOpShell(IReadOnlyList<string> arguments)
        {
            var values = new OptionValues();
            var options = CreateOptionSet(
                values,
                includeInteractive: false,
                includeLogin: true,
                includeLifecycle: false);
            var error = ParseOptions(options, arguments);

            if (error is not null)
            {
                return CommandLineParseResult.Failure(error);
            }

            error = ValidateCommonOptions(values);

            if (error is not null)
            {
                return CommandLineParseResult.Failure(error);
            }

            if (values.ShowHelp)
            {
                return Success(InvocationMode.OpShell, InvocationAction.ShowHelp, values);
            }

            if (values.ShowVersion)
            {
                return Success(InvocationMode.OpShell, InvocationAction.ShowVersion, values);
            }

            if (values.ShowLicenses)
            {
                return Success(InvocationMode.OpShell, InvocationAction.ShowLicenses, values);
            }

            return CommandLineParseResult.Success(
                new InvocationRequest(
                    InvocationMode.OpShell,
                    InvocationAction.StartShell,
                    null,
                    null,
                    values.Login,
                    values.Verbose,
                    values.Quiet));
        }

        private static CommandLineParseResult ParseOpSsh(IReadOnlyList<string> arguments)
        {
            if (arguments.Count == 1)
            {
                var standaloneAction = arguments[0] switch
                {
                    "--help" => InvocationAction.ShowHelp,
                    "--version" => InvocationAction.ShowVersion,
                    "--licenses" => InvocationAction.ShowLicenses,
                    _ => (InvocationAction?)null
                };

                if (standaloneAction is not null)
                {
                    return CommandLineParseResult.Success(
                        new InvocationRequest(
                            InvocationMode.OpSsh,
                            standaloneAction.Value,
                            null,
                            null,
                            loginShell: false,
                            verbose: false,
                            quiet: false));
                }
            }

            if (arguments.Count > 0 && arguments[0] == "--agent")
            {
                return ParseOpSshAgent(arguments.Skip(1));
            }

            return CommandLineParseResult.Success(
                new InvocationRequest(
                    InvocationMode.OpSsh,
                    InvocationAction.ExecuteCommand,
                    "ssh",
                    arguments.ToArray(),
                    loginShell: false,
                    verbose: false,
                    quiet: false));
        }

        private static CommandLineParseResult ParseOpSshAgent(IEnumerable<string> arguments)
        {
            var values = new AgentOptionValues();
            var error = ParseOptions(CreateAgentOptionSet(values), arguments);

            if (error is not null)
            {
                return CommandLineParseResult.Failure(error);
            }

            if (values.ShowHelp)
            {
                return AgentSuccess(InvocationAction.ShowAgentHelp, values);
            }

            if (values.ShowVersion)
            {
                return AgentSuccess(InvocationAction.ShowVersion, values);
            }

            if (values.ShowLicenses)
            {
                return AgentSuccess(InvocationAction.ShowLicenses, values);
            }

            var agentError = ValidateAgentOptions(values);

            if (agentError is not null)
            {
                return CommandLineParseResult.Failure(agentError);
            }

            if (values.Stop)
            {
                return AgentSuccess(InvocationAction.StopAgent, values);
            }

            if (values.StopAll)
            {
                return AgentSuccess(InvocationAction.StopAllAgents, values);
            }

            return AgentSuccess(InvocationAction.StartAgent, values);
        }

        private static int FindChildIndex(
            IReadOnlyList<string> arguments,
            ICollection<string> optionArguments)
        {
            var index = 0;

            while (index < arguments.Count)
            {
                var argument = arguments[index];

                if (argument == "--")
                {
                    return index + 1;
                }

                if (argument == "-" || argument[0] != '-')
                {
                    return index;
                }

                optionArguments.Add(argument);
                index++;
            }

            return index;
        }

        private static string? ParseOptions(
            OptionSet options,
            IEnumerable<string> arguments)
        {
            try
            {
                var remaining = options.Parse(arguments);
                return remaining.Count == 0
                    ? null
                    : $"Unexpected argument: {remaining[0]}";
            }
            catch (OptionException exception)
            {
                return exception.Message;
            }
        }

        private static string? ValidateCommonOptions(OptionValues values)
        {
            if (values.Verbose && values.Quiet)
            {
                return "The --verbose and --quiet options cannot be used together.";
            }

            return null;
        }

        private static string? ValidateLifecycleOptions(
            OptionValues values,
            bool hasChild)
        {
            var lifecycleActionCount =
                (values.Install ? 1 : 0) +
                (values.Update ? 1 : 0) +
                (values.Uninstall ? 1 : 0);

            if (lifecycleActionCount > 1)
            {
                return "The --install, --update, and --uninstall options cannot be used together.";
            }

            var hasLifecycleAction = lifecycleActionCount != 0;

            if ((values.UserInstallation || values.Force || values.AssumeYes) &&
                !hasLifecycleAction)
            {
                return "The --user, --force, and --yes options require an installation action.";
            }

            if (values.Force && !values.Install)
            {
                return "The --force option can be used only with --install.";
            }

            if (values.AssumeYes && !values.Update)
            {
                return "The --yes option can be used only with --update.";
            }

            if (hasLifecycleAction && (hasChild || values.Interactive))
            {
                return "Installation options cannot be combined with a child command or -i.";
            }

            if (hasLifecycleAction && (values.Verbose || values.Quiet))
            {
                return "Installation options cannot be combined with --verbose or --quiet.";
            }

            return null;
        }

        private static string? ValidateAgentOptions(AgentOptionValues values)
        {
            if (values.Stop && values.StopAll)
            {
                return "The --stop and --stop-all options cannot be used together.";
            }

            if ((values.Stop || values.StopAll) && values.Daemon)
            {
                return "Agent stop options cannot be combined with --daemon.";
            }

            return null;
        }

        private static CommandLineParseResult Success(
            InvocationMode mode,
            InvocationAction action,
            OptionValues values)
        {
            return CommandLineParseResult.Success(
                new InvocationRequest(
                    mode,
                    action,
                    null,
                    null,
                    loginShell: false,
                    values.Verbose,
                    values.Quiet));
        }

        private static OptionSet CreateOptionSet(
            OptionValues values,
            bool includeInteractive,
            bool includeLogin,
            bool includeLifecycle)
        {
            var options = new OptionSet
            {
                {
                    "v|verbose",
                    "Enable verbose, secret-safe diagnostics.",
                    value => values.Verbose = value is not null
                },
                {
                    "q|quiet",
                    "Suppress non-error wrapper output.",
                    value => values.Quiet = value is not null
                },
                {
                    "h|help",
                    "Show help and exit.",
                    value => values.ShowHelp = value is not null
                },
                {
                    "V|version",
                    "Show version and exit.",
                    value => values.ShowVersion = value is not null
                },
                {
                    "licenses",
                    "Show bundled license notices and exit.",
                    value => values.ShowLicenses = value is not null
                }
            };

            if (includeInteractive)
            {
                options.Add(
                    "i",
                    "Start an interactive non-login shell.",
                    value => values.Interactive = value is not null);
            }

            if (includeLogin)
            {
                options.Add(
                    "l|login",
                    "Start a login shell.",
                    value => values.Login = value is not null);
            }

            if (includeLifecycle)
            {
                options.Add(
                    "install",
                    "Install opexec and its aliases (system-wide by default).",
                    value => values.Install = value is not null);
                options.Add(
                    "uninstall",
                    "Remove opexec and its aliases.",
                    value => values.Uninstall = value is not null);
                options.Add(
                    "update",
                    "Check for and install the latest stable release.",
                    value => values.Update = value is not null);
                options.Add(
                    "user",
                    "Use the current user's ~/.local/bin.",
                    value => values.UserInstallation = value is not null);
                options.Add(
                    "force",
                    "Replace conflicting files during installation.",
                    value => values.Force = value is not null);
                options.Add(
                    "y|yes",
                    "Install an available update without prompting.",
                    value => values.AssumeYes = value is not null);
            }

            return options;
        }

        private static OptionSet CreateAgentOptionSet(AgentOptionValues values)
        {
            return new OptionSet
            {
                {
                    "v|verbose",
                    "Enable verbose, secret-safe diagnostics.",
                    value => values.Verbose = value is not null
                },
                {
                    "d|daemon",
                    "Run as a detached background agent.",
                    value => values.Daemon = value is not null
                },
                {
                    "stop",
                    "Stop the detached agent selected by SSH_AUTH_SOCK.",
                    value => values.Stop = value is not null
                },
                {
                    "stop-all",
                    "Stop all detached opssh agents owned by the current user.",
                    value => values.StopAll = value is not null
                },
                {
                    "h|help",
                    "Show help and exit.",
                    value => values.ShowHelp = value is not null
                },
                {
                    "V|version",
                    "Show version and exit.",
                    value => values.ShowVersion = value is not null
                },
                {
                    "licenses",
                    "Show bundled license notices and exit.",
                    value => values.ShowLicenses = value is not null
                }
            };
        }

        private static CommandLineParseResult AgentSuccess(
            InvocationAction action,
            AgentOptionValues values)
        {
            return CommandLineParseResult.Success(
                new InvocationRequest(
                    InvocationMode.OpSsh,
                    action,
                    null,
                    null,
                    loginShell: false,
                    values.Verbose,
                    quiet: false,
                    values.Daemon));
        }

        private static CommandLineParseResult LifecycleSuccess(
            InvocationAction action,
            OptionValues values)
        {
            return CommandLineParseResult.Success(
                new InvocationRequest(
                    InvocationMode.OpExec,
                    action,
                    null,
                    null,
                    loginShell: false,
                    verbose: false,
                    quiet: false,
                    daemon: false,
                    values.UserInstallation,
                    values.Force,
                    values.AssumeYes));
        }

        private sealed class OptionValues
        {
            public bool Verbose { get; set; }

            public bool Quiet { get; set; }

            public bool ShowHelp { get; set; }

            public bool ShowVersion { get; set; }

            public bool ShowLicenses { get; set; }

            public bool Interactive { get; set; }

            public bool Login { get; set; }

            public bool Install { get; set; }

            public bool Uninstall { get; set; }

            public bool Update { get; set; }

            public bool UserInstallation { get; set; }

            public bool Force { get; set; }

            public bool AssumeYes { get; set; }
        }

        private sealed class AgentOptionValues
        {
            public bool Verbose { get; set; }

            public bool Daemon { get; set; }

            public bool Stop { get; set; }

            public bool StopAll { get; set; }

            public bool ShowHelp { get; set; }

            public bool ShowVersion { get; set; }

            public bool ShowLicenses { get; set; }
        }
    }
}
