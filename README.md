# opexec

`opexec` runs commands, shells, and OpenSSH clients in a process-scoped 1Password
authentication context backed by an ephemeral SSH-agent socket.

## Project status and disclaimer

This is the source-only r12 snapshot for the first public release of OpExec.
OpExec is an independent project and is not affiliated with, endorsed by, or sponsored by 1Password.
Product names and trademarks belong to their respective
owners. The software is provided as-is under the MIT License, without warranty.
Review the [security boundaries](docs/security-hardening.md) before trusting it
with credentials or using it on systems you administer.

## Build and validation

The supported deployment target is Linux x64. Install the .NET 10 SDK to build
from source; runtime use also requires the 1Password CLI (`op`) and, for `opssh`,
OpenSSH (`ssh`) on `PATH`, with access to your configured 1Password account.

```sh
dotnet restore OpExec.slnx
dotnet build OpExec.slnx -c Release --no-restore
dotnet test OpExec.slnx -c Release --no-build
dotnet publish src/OpExec/OpExec.csproj -p:PublishProfile=linux-x64
```

The executable is produced at `src/OpExec/bin/Publish/linux-x64/opexec`.
Run validation on Linux as well: some platform-specific tests return early on
other operating systems. See [test fixtures](tests/README.md) for scope and
limitations.

## Installation and usage

The release is one self-contained executable. Install it system-wide (the default)
with:

```bash
sudo ./opexec --install
```

This copies the executable to `/usr/local/bin/opexec` and creates the `opshell` and
`opssh` aliases. A per-user installation uses `$HOME/.local/bin` instead:

```bash
./opexec --install --user
```

Use the corresponding `--uninstall` command, with `--user` when applicable, to
remove an installation. Installation refuses unrelated existing files unless
`--force` is explicitly supplied with `--install`.

```bash
opexec command argument
opshell
opssh root@example.com
```

Run an agent in the foreground:

```bash
opssh --agent
```

Start a detached agent and apply its socket to the current shell:

```bash
eval "$(opssh --agent --daemon)"
```

Stop that agent and clear its exported environment with:

```bash
eval "$(opssh --agent --stop)"
```

To stop every detached agent owned by the current user:

```bash
eval "$(opssh --agent --stop-all)"
```

Use `opssh --agent --help` for agent-mode options. Detached human sessions are not
reauthenticated after invalidation. While a manually authenticated OpExec scope is
running, it performs an authenticated, account-specific 1Password heartbeat every 5 minutes to
prevent the normal inactivity timeout, with an initial post-start check after five
seconds. If the session is revoked or cannot be
renewed after bounded retries, sign in again and restart the scope.

See [the architecture](docs/architecture.md), [foreground process execution](docs/foreground-process-execution.md),
[security hardening boundaries](docs/security-hardening.md), and
[versioning policy](docs/versioning.md) for design details.

## Contact and security

For general questions, contact [Andrew J. Moore](mailto:andrew@forevermoore.net)
or open an issue on [GitHub](https://github.com/bobapplemac/opexec/issues).
Report suspected vulnerabilities privately to the same email address; see
[SECURITY.md](SECURITY.md) for reporting guidance. Do not include credentials
or sensitive account information in public issues.

## License

OpExec is licensed under the MIT License.
See [LICENSE.txt](LICENSE.txt) for the license text. License texts and notices for
all third-party libraries and runtime components bundled into the self-contained
executable are retained under [licenses](licenses/README.md).

Print the complete embedded notice set from any installed alias with:

```bash
opexec --licenses
opshell --licenses
opssh --licenses
opssh --agent --licenses
```
