# Architecture

The release contains one self-contained executable assembled from three production
projects.

## OpExec

`OpExec` is the executable and composition root. It owns command-line parsing,
invocation-path alias dispatch, shell and foreground-process execution, signal
handling, integrated-agent supervision, detached-agent hosting, daemon lifecycle
control, and self-installation.

The same executable is invoked through three user-facing names:

- `opexec` runs an arbitrary command in a scoped 1Password and SSH-agent context.
- `opshell` starts an interactive shell in that context.
- `opssh` passes arguments through to OpenSSH, except when its first argument is the
  reserved `--agent` mode selector.

## OpExec.SshAgent

`OpExec.SshAgent` is a class library with no 1Password CLI dependency. It owns the
SSH-agent protocol, Unix socket server, runtime-directory isolation, Ed25519 key
parsing and signing, and the identity-provider abstraction.

## OpExec.OnePassword

`OpExec.OnePassword` is a class library that owns all `op` CLI interaction,
multi-account session selection, interactive sign-in, bounded secret capture, item
enumeration, process-lifetime session keepalive, and the 1Password implementation
of the SSH identity-provider abstraction.

The dependency direction is:

```text
OpExec --------------------> OpExec.OnePassword
   |                                |
   +----------> OpExec.SshAgent <---+
```

Only `OpExec` is published. Both class libraries and their package dependencies are
bundled into that single self-contained executable.

Self-installation copies that published executable atomically and creates relative
`opshell` and `opssh` symbolic links. System installation targets `/usr/local/bin`;
the explicit `--user` mode targets `$HOME/.local/bin`. Uninstallation removes only
aliases that resolve to the managed `opexec` path and refuses unverified files.

Detached-agent lifecycle control uses JSON-RPC 2.0 over a private Unix-domain
socket. Each UTF-8 JSON message is framed by a four-byte unsigned big-endian payload
length. The control protocol is implemented with the platform `System.Text.Json`
library because the current surface contains only one method; this avoids importing
a general RPC framework and its transitive dependency graph into the self-contained
executable.
