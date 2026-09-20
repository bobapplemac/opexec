// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.SshAgent
{
    public interface ISshIdentityProvider
    {
        Task<IReadOnlyList<SshIdentity>> GetIdentitiesAsync(
            CancellationToken cancellationToken);

        Task<SshSignature> SignAsync(
            SshIdentity identity,
            ReadOnlyMemory<byte> data,
            uint flags,
            CancellationToken cancellationToken);
    }
}
