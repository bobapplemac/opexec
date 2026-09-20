// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public interface IInteractiveOnePasswordSignInRunner
    {
        Task<InteractiveSignInResult> RunAsync(CancellationToken cancellationToken);
    }

    public sealed record InteractiveSignInResult(int ExitCode, string SessionToken);
}
