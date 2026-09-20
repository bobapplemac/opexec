# Foreground process execution exception

The project normally requires CliWrap for external process execution. Captured and
internal helper processes, including all `op` CLI calls, continue to use CliWrap.

`opexec` deliberately uses `System.Diagnostics.Process` for three narrow process
boundaries:

- launching the user-selected foreground child process; and
- running `op signin --raw` with inherited stdin/stderr while capturing only its
  secret stdout session token; and
- launching the detached `opssh --agent --daemon` worker while retaining an
  explicit private readiness channel.

CliWrap 3.10 redirects standard input, output, and error through anonymous pipes.
Even when those pipes copy bytes to and from the parent console, the child no longer
inherits the terminal file descriptors. On Linux this makes `isatty` false and breaks
or changes behavior for interactive programs such as SSH clients, shells, editors,
pagers, password prompts, job control, and terminal resize handling. CliWrap does not
currently provide either unredirected standard-handle inheritance or PTY allocation.

The foreground runner therefore:

- starts the child directly without shell interpretation;
- adds every argument separately through `ProcessStartInfo.ArgumentList`;
- inherits stdin, stdout, and stderr without redirection;
- preserves the current directory and inherited environment;
- returns the child exit code unchanged;
- terminates the child process tree when wrapper cancellation requires cleanup.

No output is captured or inspected at this boundary. The exception should be removed
if a future CliWrap version supports true inherited terminal handles.

The sign-in boundary redirects only stdout because `--raw` emits the session token
there. stdin and stderr remain attached to the terminal so 1Password can safely
perform its normal account, credential, and MFA interaction. The captured token is
never logged. After sign-in, opexec validates the token against configured accounts,
using the account UUID as `OP_ACCOUNT` and supplying the token through the
`OP_SESSION_<user-uuid>` variable emitted by the targeted CLI. The selected account
and token remain scoped to the `opexec` process tree.

The daemon-launch boundary must intentionally outlive its launching process, detach
from the terminal, and report socket readiness before the launcher emits shell code.
CliWrap supervises and cancels its child processes by design, which conflicts with
that ownership transfer. The launcher therefore uses `Process` directly, passes
arguments through `ArgumentList`, redirects all three standard handles away from the
detached worker, and exchanges one bounded internal readiness record. No secret is
placed in an argument or readiness record. All ordinary short-lived helper commands
continue to use CliWrap.
