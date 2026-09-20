// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        OnePasswordClient.cs
// Revision:    r9
// Modified:    2026-09-20
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Typed, bounded interface to the 1Password CLI for version probing, account and
//              SSH-key enumeration, private-key retrieval, authentication validation, and
//              account-specific session keepalive operations.
// ------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using OpExec.SshAgent;

namespace OpExec.OnePassword
{
    public sealed class OnePasswordClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _executablePath;
        private readonly IReadOnlyDictionary<string, string?> _environmentVariables;
        private readonly IOnePasswordCommandRunner _commandRunner;

        public OnePasswordClient(OnePasswordClientOptions? options = null)
            : this(options ?? new OnePasswordClientOptions(), new OnePasswordCommandRunner())
        {
        }

        internal OnePasswordClient(
            OnePasswordClientOptions options,
            IOnePasswordCommandRunner commandRunner)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(commandRunner);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.ExecutablePath);
            ArgumentNullException.ThrowIfNull(options.EnvironmentVariables);

            _executablePath = options.ExecutablePath;
            _environmentVariables = new Dictionary<string, string?>(
                options.EnvironmentVariables,
                StringComparer.Ordinal);
            _commandRunner = commandRunner;
        }

        public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
        {
            var result = await RunAsync(new[] { "--version" }, "version check", cancellationToken);
            var version = result.StandardOutput.Trim();

            if (version.Length == 0)
            {
                throw new InvalidDataException(
                    "The 1Password CLI version check returned no version.");
            }

            return version;
        }

        public async Task<bool> IsAuthenticatedAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await _commandRunner.RunAsync(
                _executablePath,
                new[] { "whoami" },
                _environmentVariables,
                cancellationToken);
            return result.ExitCode == 0;
        }

        public async Task<bool> KeepSessionAliveAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await KeepSessionAliveWithDiagnosticsAsync(cancellationToken);
            }
            catch (OnePasswordHeartbeatException)
            {
                return false;
            }
        }

        internal async Task<bool> KeepSessionAliveWithDiagnosticsAsync(
            CancellationToken cancellationToken = default)
        {
            var arguments = new List<string>
            {
                "vault",
                "list",
                "--format",
                "json"
            };
            var account = GetEffectiveEnvironmentVariable("OP_ACCOUNT");

            if (!string.IsNullOrWhiteSpace(account))
            {
                arguments.Add("--account");
                arguments.Add(account);
            }

            var result = await _commandRunner.RunStatusAsync(
                _executablePath,
                arguments,
                _environmentVariables,
                cancellationToken);

            if (result.ExitCode == 0)
            {
                return true;
            }

            throw new OnePasswordHeartbeatException(
                CreateHeartbeatFailureMessage(result.ExitCode, result.StandardError));
        }

        public async Task<IReadOnlyList<OnePasswordAccount>> ListAccountsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await RunAsync(
                new[] { "account", "list", "--format", "json" },
                "account enumeration",
                cancellationToken);

            List<AccountListEntry>? entries;

            try
            {
                entries = JsonSerializer.Deserialize<List<AccountListEntry>>(
                    result.StandardOutput,
                    JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The 1Password CLI returned invalid JSON while listing accounts.",
                    exception);
            }

            if (entries is null)
            {
                throw new InvalidDataException(
                    "The 1Password CLI returned no account list.");
            }

            var accounts = new List<OnePasswordAccount>(entries.Count);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.AccountId) ||
                    string.IsNullOrWhiteSpace(entry.UserId))
                {
                    throw new InvalidDataException(
                        "The 1Password CLI returned an account without an account ID or user ID.");
                }

                accounts.Add(new OnePasswordAccount(entry.AccountId, entry.UserId));
            }

            return accounts
                .GroupBy(account => account.AccountId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(account => account.AccountId, StringComparer.Ordinal)
                .ToArray();
        }

        public async Task<IReadOnlyList<OnePasswordItem>> ListSshKeyItemsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await RunAsync(
                new[] { "item", "list", "--categories", "SSH Key", "--format", "json" },
                "SSH Key item enumeration",
                cancellationToken);

            List<ItemListEntry>? entries;

            try
            {
                entries = JsonSerializer.Deserialize<List<ItemListEntry>>(
                    result.StandardOutput,
                    JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The 1Password CLI returned invalid JSON while listing SSH Key items.",
                    exception);
            }

            if (entries is null)
            {
                throw new InvalidDataException(
                    "The 1Password CLI returned no item list.");
            }

            var items = new List<OnePasswordItem>(entries.Count);

            foreach (var entry in entries)
            {
                if (!string.Equals(entry.Category, "SSH_KEY", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Id) ||
                    string.IsNullOrWhiteSpace(entry.Title) ||
                    string.IsNullOrWhiteSpace(entry.Vault?.Id))
                {
                    throw new InvalidDataException(
                        "The 1Password CLI returned an SSH Key item without an ID, title, or vault ID.");
                }

                items.Add(new OnePasswordItem(entry.Vault.Id, entry.Id, entry.Title));
            }

            return items
                .OrderBy(item => item.VaultId, StringComparer.Ordinal)
                .ThenBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray();
        }

        public async Task<string> GetPublicKeyAsync(
            OnePasswordItem item,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(item);

            var result = await RunAsync(
                new[]
                {
                    "item",
                    "get",
                    item.ItemId,
                    "--vault",
                    item.VaultId,
                    "--fields",
                    "label=public key",
                    "--format",
                    "json"
                },
                "public-key retrieval",
                cancellationToken);

            try
            {
                using var document = JsonDocument.Parse(result.StandardOutput);
                var publicKey = FindPublicKeyValue(document.RootElement);

                if (string.IsNullOrWhiteSpace(publicKey))
                {
                    throw new InvalidDataException(
                        $"SSH Key item '{item.ItemId}' has no public key value.");
                }

                return publicKey;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"The 1Password CLI returned invalid JSON for SSH Key item '{item.ItemId}'.",
                    exception);
            }
        }

        internal async Task<SecretBuffer> GetPrivateKeyAsync(
            OnePasswordItem item,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(item);

            var reference =
                $"op://{item.VaultId}/{item.ItemId}/private key?ssh-format=openssh";
            var result = await _commandRunner.RunSecretAsync(
                _executablePath,
                new[] { "read", reference, "--no-newline" },
                _environmentVariables,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                result.StandardOutput.Dispose();
                throw new InvalidOperationException(
                    $"The 1Password CLI private-key retrieval failed with exit code {result.ExitCode}. " +
                    "Run op read directly for authentication or diagnostic details.");
            }

            if (result.StandardOutput.IsEmpty)
            {
                result.StandardOutput.Dispose();
                throw new InvalidDataException(
                    $"SSH Key item '{item.ItemId}' returned no private key.");
            }

            return result.StandardOutput;
        }

        private async Task<OnePasswordCommandResult> RunAsync(
            IReadOnlyList<string> arguments,
            string operation,
            CancellationToken cancellationToken)
        {
            var result = await _commandRunner.RunAsync(
                _executablePath,
                arguments,
                _environmentVariables,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"The 1Password CLI {operation} failed with exit code {result.ExitCode}. " +
                    "Run the equivalent op command directly for authentication or diagnostic details.");
            }

            return result;
        }

        private string? GetEffectiveEnvironmentVariable(string name)
        {
            return _environmentVariables.TryGetValue(name, out var value)
                ? value
                : Environment.GetEnvironmentVariable(name);
        }

        private string CreateHeartbeatFailureMessage(int exitCode, string standardError)
        {
            var diagnostic = standardError.Trim();

            foreach (var name in GetSensitiveEnvironmentVariableNames())
            {
                var value = GetEffectiveEnvironmentVariable(name);

                if (!string.IsNullOrEmpty(value))
                {
                    diagnostic = diagnostic.Replace(
                        value,
                        "[REDACTED]",
                        StringComparison.Ordinal);
                }
            }

            diagnostic = diagnostic
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

            if (diagnostic.Length > 512)
            {
                diagnostic = diagnostic[..512] + "...";
            }

            return diagnostic.Length == 0
                ? $"op vault list exited with code {exitCode} without diagnostic output."
                : $"op vault list exited with code {exitCode}: {diagnostic}";
        }

        private IEnumerable<string> GetSensitiveEnvironmentVariableNames()
        {
            var names = _environmentVariables.Keys
                .Concat(
                    Environment.GetEnvironmentVariables()
                        .Keys
                        .OfType<string>())
                .Distinct(StringComparer.Ordinal);

            return names.Where(name =>
                string.Equals(name, "OP_SESSION", StringComparison.Ordinal) ||
                name.StartsWith("OP_SESSION_", StringComparison.Ordinal) ||
                string.Equals(name, "OP_CONNECT_TOKEN", StringComparison.Ordinal) ||
                string.Equals(
                    name,
                    "OP_SERVICE_ACCOUNT_TOKEN",
                    StringComparison.Ordinal));
        }

        private static string? FindPublicKeyValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                {
                    var value = FindPublicKeyValue(child);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (IsPublicKeyField(element) &&
                element.TryGetProperty("value", out var valueElement) &&
                valueElement.ValueKind == JsonValueKind.String)
            {
                return valueElement.GetString();
            }

            if (element.TryGetProperty("fields", out var fieldsElement))
            {
                return FindPublicKeyValue(fieldsElement);
            }

            return null;
        }

        private static bool IsPublicKeyField(JsonElement element)
        {
            return PropertyEquals(element, "id", "public_key") ||
                PropertyEquals(element, "label", "public key");
        }

        private static bool PropertyEquals(
            JsonElement element,
            string propertyName,
            string expectedValue)
        {
            return element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                string.Equals(
                    property.GetString(),
                    expectedValue,
                    StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ItemListEntry
        {
            public string? Id { get; init; }

            public string? Title { get; init; }

            public string? Category { get; init; }

            public ItemVault? Vault { get; init; }
        }

        private sealed class ItemVault
        {
            public string? Id { get; init; }
        }

        private sealed class AccountListEntry
        {
            [JsonPropertyName("account_uuid")]
            public string? AccountId { get; init; }

            [JsonPropertyName("user_uuid")]
            public string? UserId { get; init; }
        }
    }
}
