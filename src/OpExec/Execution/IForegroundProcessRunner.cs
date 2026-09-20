// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec
{
    internal interface IForegroundProcessRunner
    {
        Task<int> RunAsync(
            ForegroundProcessRequest request,
            CancellationToken cancellationToken);
    }
}
