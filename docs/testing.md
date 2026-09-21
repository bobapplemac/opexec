# Public test fixtures

All embedded keys in this directory are intentionally public test fixtures.
Treat them as compromised. Never add them to a real `authorized_keys` file, import
them as a usable credential, or use them to protect real systems or data.

- `src/OpExec.SshAgent.Tests/OpenSshTestKey.cs` contains an unencrypted Ed25519 OpenSSH
  private key and its public key, with comment `opexec-milestone-6-test-key`.
- `src/OpExec.OnePassword.Tests/OpenSshTestKey.cs` contains the same private key for
  fake 1Password responses.
- `src/OpExec.SshAgent.Tests/TestIdentityProvider.cs` contains a deterministic public
  Ed25519 seed, with comment `opexec-milestone-4-test-key`, and derives test keys
  from it. These are test-assembly fixtures, not production credentials.
- Malformed PEM strings in signer tests and strings such as `session-token`,
  `account-id`, `vault-id`, and `item-id` are synthetic test inputs.

Secret scanners may flag these fixtures. Review each finding against the exact
fixture location; do not suppress private-key detection across the repository.
If any fixture was ever authorized on a real system, remove that authorization
and replace the credential before publication.

Run `dotnet test src/OpExec.slnx -c Release` on both Windows and Linux. The suite uses
fake 1Password responses; it does not validate live account authentication.
Linux-only installation and daemon-control tests return early on Windows, Unix
socket tests return early when sockets are unavailable, and Unix permission
assertions are not exercised on Windows. Test runner pass totals include those
early returns.
