# opexec

`opexec` runs commands, shells, and OpenSSH clients in a process-scoped 1Password
authentication context backed by an ephemeral SSH-agent socket.

## Project disclaimer

OpExec is an independent project and is not affiliated with, endorsed by, or sponsored by 1Password.
Product names and trademarks belong to their respective
owners. The software is provided as-is under the MIT License, without warranty.
Review the [security boundaries](docs/security-hardening.md) before trusting it
with credentials or using it on systems you administer.

## Build and validation

The supported deployment target is Linux x64. Building requires Make plus either
the .NET 10 SDK or Docker. Runtime use also requires the 1Password CLI (`op`) and,
for `opssh`, OpenSSH (`ssh`) on `PATH`, with access to your configured 1Password
account.

On Linux, the usual build is:

```sh
make
```

This publishes the self-contained, single-file executable at
`artifacts/publish/linux-x64/opexec`. Publishing means creating a deployable
local artifact; it does not upload the binary or create a GitHub Release.

The Make workflow automatically prefers a locally installed .NET 10 SDK so it
benefits from the normal NuGet cache and has the shortest edit/build cycle. If
the SDK is unavailable and Docker is installed, it uses the pinned SDK container
instead. This selection applies to `make build`, `make test`, and `make publish`.
The explicit `native-*` and `docker-*` targets can force either backend:

```sh
make publish BUILD_BACKEND=dotnet
make publish BUILD_BACKEND=docker

# Equivalent convenience targets:
make native-publish
make docker-publish
```

`BUILD_BACKEND` accepts `auto` (the default), `dotnet`, or `docker`, and can also
be used with `make build` and `make test`.

`make build` places compiler output under `artifacts/bin`; it is intermediate
build output and is not the installable single-file executable. `make publish`
and the Visual Studio `linux-x64` folder profile both place the deployable binary
under `artifacts/publish/linux-x64`. Run `make help` for the complete target list.

## GitHub releases

`publish` retains its standard .NET meaning: it creates a local deployable
application. Creating a public GitHub Release is a separate, explicit operation:

```sh
gh auth login
make release
```

`make release` requires a clean Linux checkout whose `HEAD` exactly matches
`origin/main`. It runs the complete test suite, publishes the Linux x64 binary,
packages it as `artifacts/release/opexec-rN-linux-x64.tar.gz`, writes a SHA-256
checksum beside it, and creates the corresponding `rN` Git tag and GitHub
Release using the revision in `src/Directory.Build.props`. `GH_TOKEN` may be
used instead of an interactive `gh auth login` session.

Published revisions are immutable. The release command refuses to replace an
existing tag or GitHub Release; increment `ProductRevision` for a subsequent
release.

The Docker backend requires Docker BuildKit and network access to restore NuGet
packages and pull the SDK image on its first run.

The equivalent direct .NET commands, which remain suitable for Visual Studio
and Windows development, are:

```sh
dotnet restore src/OpExec.slnx
dotnet build src/OpExec.slnx -c Release --artifacts-path artifacts
dotnet test src/OpExec.slnx -c Release --artifacts-path artifacts
dotnet publish src/OpExec/OpExec.csproj -p:PublishProfile=linux-x64
```

Run validation on Linux as well: some platform-specific tests return early on
other operating systems. See [test fixtures](docs/testing.md) for scope and
limitations.

## Installation and usage

The release is one self-contained executable. Build and install it system-wide
with the traditional Make workflow:

```bash
make
sudo make install
```

The unprivileged `make` step publishes the local binary. The privileged install
step copies it to `/usr/local/bin/opexec` and creates the `opshell` and `opssh`
aliases without rebuilding. Remove the system installation with:

```bash
sudo make uninstall
```

The Makefile does not invoke `sudo`; privilege elevation remains under the
user's control.

To install a previously published or downloaded binary directly, run
`sudo ./opexec --install`. A per-user installation uses `$HOME/.local/bin`
instead:

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
executable are retained in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Print the complete embedded notice set from any installed alias with:

```bash
opexec --licenses
opshell --licenses
opssh --licenses
opssh --agent --licenses
```
