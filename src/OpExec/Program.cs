// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        Program.cs
// Revision:    r5
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Application composition root for command-line dispatch, lifecycle actions,
//              1Password authentication and keepalive, integrated SSH-agent supervision,
//              foreground execution, license reporting, cancellation, and exit handling.
// ------------------------------------------------------------------------------------------

using OpExec.OnePassword;

namespace OpExec
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (AgentDaemonWorker.IsInvocation(args))
            {
                return await RunDaemonWorkerAsync(args);
            }

            var invocationPath = InvocationPathResolver.Resolve();
            var parser = new CommandLineParser();
            var result = parser.Parse(invocationPath, args);

            if (!result.IsSuccess)
            {
                var errorPrefix = IsAgentInvocation(invocationPath, args)
                    ? "opssh"
                    : "opexec";
                Console.Error.WriteLine($"{errorPrefix}: {result.Error}");
                if (errorPrefix == "opssh")
                {
                    parser.WriteAgentUsage(Console.Error);
                }
                else
                {
                    parser.WriteUsage(
                        Console.Error,
                        CommandLineParser.ResolveMode(invocationPath));
                }
                return 2;
            }

            var invocation = result.Request!;
            var logPrefix = IsAgentAction(invocation.Action)
                ? "opssh"
                : "opexec";

            switch (invocation.Action)
            {
                case InvocationAction.ShowHelp:
                    parser.WriteUsage(Console.Out, invocation.Mode);
                    return 0;
                case InvocationAction.ShowAgentHelp:
                    parser.WriteAgentUsage(Console.Out);
                    return 0;
                case InvocationAction.ShowVersion:
                    Console.Out.WriteLine(CommandLineParser.GetVersion());
                    return 0;
                case InvocationAction.ShowLicenses:
                    LicenseNoticeWriter.Write(Console.Out);
                    return 0;
                case InvocationAction.ShowUsage:
                    parser.WriteUsage(Console.Out, invocation.Mode);
                    return 0;
            }

            using var shutdown = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            using var signalRegistration = new ShutdownSignalRegistration(shutdown);

            try
            {
                Action<string>? log = invocation.Verbose
                    ? message => Console.Error.WriteLine($"{logPrefix}: {message}")
                    : null;

                if (invocation.Action is
                    InvocationAction.Install or InvocationAction.Uninstall)
                {
                    return new SelfInstallationManager().Run(invocation);
                }

                if (invocation.Action == InvocationAction.Update)
                {
                    return await new SelfUpdateManager().RunAsync(
                        invocation,
                        shutdown.Token);
                }

                if (invocation.Action is
                    InvocationAction.StopAgent or InvocationAction.StopAllAgents)
                {
                    return await new AgentStopRunner(
                            new AgentDaemonController())
                        .RunAsync(invocation, log, shutdown.Token);
                }

                var authenticationManager = new OnePasswordAuthenticationManager(
                    new OnePasswordAuthenticationProbe(),
                    new InteractiveOnePasswordSignInRunner(),
                    invocation.Quiet
                        ? null
                        : message => Console.Error.WriteLine(message),
                    log);

                if (invocation.Action == InvocationAction.StartAgent)
                {
                    return await new AgentModeRunner(
                            authenticationManager,
                            new AgentDaemonLauncher())
                        .RunAsync(invocation, log, shutdown.Token);
                }

                var execution = new ExecutionPlanner(new ShellResolver())
                    .CreateRequest(invocation);
                return await new ExecutionSupervisor(
                        authenticationManager,
                        new IntegratedSshAgentFactory(),
                        new ForegroundProcessRunner())
                    .RunAsync(
                        execution,
                        log,
                        shutdown.Token,
                        message => Console.Error.WriteLine($"opexec: {message}"));
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                return 130;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"{logPrefix}: {exception.Message}");
                return 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static bool IsAgentInvocation(
            string invocationPath,
            IReadOnlyList<string> arguments)
        {
            return CommandLineParser.ResolveMode(invocationPath) ==
                    InvocationMode.OpSsh &&
                arguments.FirstOrDefault() == "--agent";
        }

        private static bool IsAgentAction(InvocationAction action)
        {
            return action is
                InvocationAction.StartAgent or
                InvocationAction.StopAgent or
                InvocationAction.StopAllAgents or
                InvocationAction.ShowAgentHelp;
        }

        private static async Task<int> RunDaemonWorkerAsync(string[] args)
        {
            using var shutdown = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            using var signalRegistration = new ShutdownSignalRegistration(shutdown);

            try
            {
                return await AgentDaemonWorker.RunAsync(args, shutdown.Token);
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
    }
}
