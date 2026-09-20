// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public sealed class OnePasswordAccount
    {
        public OnePasswordAccount(string accountId, string userId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            AccountId = accountId;
            UserId = userId;
        }

        public string AccountId { get; }

        public string UserId { get; }
    }
}
