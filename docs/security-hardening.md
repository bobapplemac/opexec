# Security hardening boundaries

The initial hardening milestone makes the following limits and lifecycle guarantees
explicit.

## Runtime isolation

- Every agent instance uses a cryptographically random per-run directory.
- Unix runtime directories are set to mode `0700` and verified after creation.
- Unix socket files are set to mode `0600` and verified before listening is announced.
- Detached agents expose a separate mode `0600` lifecycle-control socket inside the
  same mode `0700` per-run directory.
- Lifecycle messages use JSON-RPC 2.0 envelopes with a four-byte unsigned big-endian
  length prefix. Payloads are limited to 4096 bytes and read to the declared length,
  preventing delimiter ambiguity and unbounded allocation.
- Partial startup failures remove a newly created per-run directory.
- Runtime-directory cleanup runs even if server shutdown reports an unexpected error.

## Protocol limits

- SSH-agent request packets are limited to 256 KiB by default.
- Responses that exceed the configured packet limit are replaced with
  `SSH_AGENT_FAILURE`.
- Empty, truncated, oversized, trailing-data, and unsupported requests fail without
  terminating the agent server.
- Concurrent client connections are isolated from one another.
- RSA signing follows the algorithm requested by the SSH client: SHA-256 or SHA-512
  through the standard agent flags, and legacy SHA-1 when neither flag is present.
  Whether legacy RSA/SHA-1 is permitted remains an OpenSSH client/server policy
  decision.

## Secret handling

- Private-key stdout is captured in mutable buffers, limited to 1 MiB, and zeroed on
  disposal and buffer growth.
- Interactive sign-in stdout is limited to 16 KiB. Temporary mutable character
  buffers are cleared after capture. Application-held references to the resulting
  immutable .NET string are cleared with the process-scoped context, although the
  runtime may retain immutable string storage until garbage collection.
- Private keys are fetched for one signing operation, never written to disk, and
  never cached.
- Discarded helper-process output is connected to an explicit null stream so a
  detached worker never passes a closed inherited standard handle to `op`.
- Provider exception messages are not copied into agent logs. Only the exception
  type is recorded.
- Verbose authentication logging identifies an inherited or created context without
  logging account credentials or session values.
- A manually authenticated context with an effective `OP_SESSION` variable performs
  an authenticated `op vault list` heartbeat every 5 minutes. Its normal JSON output
  is discarded through an explicitly opened null stream. The selected `OP_ACCOUNT`
  is passed explicitly when available. Session values are supplied only through the
  child environment and never through command arguments or diagnostic output. An
  initial check five seconds after startup detects post-detachment failures quickly.
- Service-account, Connect, and desktop-app authentication do not start the manual
  session heartbeat. Three consecutive heartbeat failures, with one-minute retry
  intervals, confirm invalidation; OpExec never performs unattended reauthentication.

## Cancellation and signals

- `opexec` and foreground `opssh --agent` handle Ctrl+C.
- Both Linux hosts register SIGTERM and cancel their supervised work.
- Cancellation stops foreground child process trees and disposes the integrated
  agent and owned authentication context.
- Foreground, detached, and integrated agent shutdown waits for active client handlers before
  removing the socket directory.

## Detached agent mode

- `opssh --agent --daemon` performs interactive authentication before detaching.
- The launcher prints an export statement only after the worker reports that its
  private agent and lifecycle-control sockets are ready. The same statement exports
  a namespaced worker PID for diagnostics after shell evaluation.
- `opssh --agent --stop` addresses only the managed daemon selected by
  `SSH_AUTH_SOCK`; `--stop-all` discovers control sockets only within the current
  user's managed runtime locations. Both commands use the private control protocol
  rather than sending a signal to an unchecked PID.
- Authentication values are never placed in command-line arguments or readiness
  output.
- The worker creates a new Linux session and redirects standard handles away from
  the invoking terminal.
- Verbose diagnostics are written to a mode `0600` file below the user's state
  directory. Non-verbose mode creates that file only when a fatal failure must be
  recorded.
- The detached worker maintains its inherited manual 1Password session while it is
  running. Confirmed keepalive failure or authentication invalidation during a
  private-key read terminates the agent and records instructions to sign in and
  restart; detached reauthentication is intentionally unsupported.

## Self-installation

- System installation requires effective user ID zero and does not invoke `sudo`
  internally. Per-user installation must be requested explicitly with `--user`.
- `--user` is rejected when the process is running through `sudo`, preventing an
  accidental installation below root's home directory.
- The executable is copied to a temporary file, hash-verified, assigned mode `0755`,
  and atomically renamed into place. Aliases are relative symbolic links created by
  the same temporary-name-and-rename pattern.
- Directories are never overwritten. Unrelated files require explicit `--force` on
  install, and uninstall refuses to remove aliases it cannot verify as managed.
