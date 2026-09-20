// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class ExecutionPlanner
    {
        private readonly ShellResolver _shellResolver;

        public ExecutionPlanner(ShellResolver shellResolver)
        {
            ArgumentNullException.ThrowIfNull(shellResolver);
            _shellResolver = shellResolver;
        }

        public ForegroundProcessRequest CreateRequest(InvocationRequest invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            if (invocation.Action == InvocationAction.ExecuteCommand)
            {
                if (string.IsNullOrWhiteSpace(invocation.Command))
                {
                    throw new InvalidOperationException(
                        "The execution request has no child command.");
                }

                return new ForegroundProcessRequest(
                    invocation.Command,
                    invocation.Arguments,
                    Environment.CurrentDirectory);
            }

            if (invocation.Action == InvocationAction.StartShell)
            {
                var shell = _shellResolver.Resolve(invocation.LoginShell);
                return new ForegroundProcessRequest(
                    shell.ExecutablePath,
                    shell.Arguments,
                    Environment.CurrentDirectory);
            }

            throw new InvalidOperationException(
                $"Invocation action '{invocation.Action}' does not execute a child process.");
        }
    }
}
