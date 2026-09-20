// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        OnePasswordIdentityProvider.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Indexes supported 1Password SSH-key items and provides on-demand identity
//              enumeration and private-key signing to the protocol-independent SSH-agent library.
// ------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using OpExec.SshAgent;

namespace OpExec.OnePassword
{
    public sealed class OnePasswordIdentityProvider : ISshIdentityProvider
    {
        private readonly IReadOnlyList<SshIdentity> _identities;
        private readonly IReadOnlyDictionary<string, OnePasswordItem> _itemsByPublicKey;
        private readonly OnePasswordClient _client;
        private readonly Action<string>? _log;
        private readonly Action? _authenticationInvalidated;

        private OnePasswordIdentityProvider(
            IReadOnlyList<SshIdentity> identities,
            IReadOnlyDictionary<string, OnePasswordItem> itemsByPublicKey,
            OnePasswordClient client,
            Action<string>? log,
            Action? authenticationInvalidated)
        {
            _identities = identities;
            _itemsByPublicKey = itemsByPublicKey;
            _client = client;
            _log = log;
            _authenticationInvalidated = authenticationInvalidated;
        }

        public static async Task<OnePasswordIdentityProvider> CreateAsync(
            OnePasswordClient client,
            Action<string>? log = null,
            CancellationToken cancellationToken = default,
            Action? authenticationInvalidated = null)
        {
            ArgumentNullException.ThrowIfNull(client);

            var version = await client.GetVersionAsync(cancellationToken);
            log?.Invoke($"1Password CLI available: {version}");

            var items = await client.ListSshKeyItemsAsync(cancellationToken);
            var identities = new List<SshIdentity>(items.Count);
            var itemsByPublicKey = new Dictionary<string, OnePasswordItem>(StringComparer.Ordinal);
            var unsupportedCount = 0;
            var duplicateCount = 0;

            foreach (var item in items)
            {
                var publicKey = await client.GetPublicKeyAsync(item, cancellationToken);

                if (!OpenSshPublicKey.TryParseEd25519(publicKey, out var publicKeyBlob))
                {
                    unsupportedCount++;
                    continue;
                }

                var indexKey = Convert.ToBase64String(publicKeyBlob);

                if (!itemsByPublicKey.TryAdd(indexKey, item))
                {
                    duplicateCount++;
                    continue;
                }

                identities.Add(new SshIdentity(
                    SshAlgorithms.Ed25519,
                    publicKeyBlob,
                    item.Title));
            }

            log?.Invoke($"1Password SSH identities indexed: {identities.Count}");

            if (unsupportedCount > 0)
            {
                log?.Invoke($"1Password SSH identities skipped as unsupported or invalid: {unsupportedCount}");
            }

            if (duplicateCount > 0)
            {
                log?.Invoke($"duplicate 1Password SSH identities skipped: {duplicateCount}");
            }

            return new OnePasswordIdentityProvider(
                identities.AsReadOnly(),
                itemsByPublicKey,
                client,
                log,
                authenticationInvalidated);
        }

        public Task<IReadOnlyList<SshIdentity>> GetIdentitiesAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_identities);
        }

        public async Task<SshSignature> SignAsync(
            SshIdentity identity,
            ReadOnlyMemory<byte> data,
            uint flags,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            cancellationToken.ThrowIfCancellationRequested();

            var indexKey = Convert.ToBase64String(identity.PublicKeyBlob.Span);

            if (!_itemsByPublicKey.TryGetValue(indexKey, out var item))
            {
                throw new InvalidOperationException("The requested identity is not available.");
            }

            var fingerprint = CreateFingerprint(identity.PublicKeyBlob.Span);
            _log?.Invoke($"sign requested: {fingerprint}");
            SecretBuffer privateKey;

            try
            {
                privateKey = await _client.GetPrivateKeyAsync(item, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (!await IsAuthenticationValidAsync(cancellationToken))
                {
                    _authenticationInvalidated?.Invoke();
                }

                throw;
            }

            using (privateKey)
            {
                var signature = Ed25519OpenSshSigner.Sign(privateKey, identity, data.Span);
                _log?.Invoke($"sign succeeded: {fingerprint}");
                return signature;
            }
        }

        private async Task<bool> IsAuthenticationValidAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                return await _client.IsAuthenticatedAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private static string CreateFingerprint(ReadOnlySpan<byte> publicKeyBlob)
        {
            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(publicKeyBlob, hash);
            return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
        }
    }
}
