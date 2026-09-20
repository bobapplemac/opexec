// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    public sealed class OnePasswordItem
    {
        public OnePasswordItem(string vaultId, string itemId, string title)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(vaultId);
            ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            if (ContainsSecretReferenceDelimiter(vaultId))
            {
                throw new ArgumentException(
                    "A stable vault ID cannot contain secret-reference delimiters.",
                    nameof(vaultId));
            }

            if (ContainsSecretReferenceDelimiter(itemId))
            {
                throw new ArgumentException(
                    "A stable item ID cannot contain secret-reference delimiters.",
                    nameof(itemId));
            }

            VaultId = vaultId;
            ItemId = itemId;
            Title = title;
        }

        public string VaultId { get; }

        public string ItemId { get; }

        public string Title { get; }

        private static bool ContainsSecretReferenceDelimiter(string value)
        {
            return value.IndexOfAny(new[] { '/', '?', '#' }) >= 0;
        }
    }
}
