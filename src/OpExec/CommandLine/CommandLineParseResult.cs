// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal sealed class CommandLineParseResult
    {
        private CommandLineParseResult(InvocationRequest? request, string? error)
        {
            Request = request;
            Error = error;
        }

        public bool IsSuccess => Request is not null;

        public InvocationRequest? Request { get; }

        public string? Error { get; }

        public static CommandLineParseResult Success(InvocationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return new CommandLineParseResult(request, null);
        }

        public static CommandLineParseResult Failure(string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            return new CommandLineParseResult(null, error);
        }
    }
}
