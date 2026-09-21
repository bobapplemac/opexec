# Versioning

OpExec uses one globally monotonic release revision for the entire repository and
solution. All production and test projects share the same release version:

- `OpExec`
- `OpExec.OnePassword`
- `OpExec.SshAgent`
- `OpExec.Tests`
- `OpExec.OnePassword.Tests`
- `OpExec.SshAgent.Tests`

User-facing versions are written as `rN`, such as `r11` or `r22`. The revision
increases once for each release containing a code, test, build, packaging, or
embedded-license change. Revisions never decrease or reset, including when the
major version changes. Documentation-only commits do not require a new revision
unless they are intentionally published as a new release.

Microsoft and .NET metadata use `Major.Revision.0` or `Major.Revision.0.0` as
required by the corresponding field. For revision 22 of major version 1:

```text
Version:              1.22.0
AssemblyVersion:      1.0.0.0
FileVersion:          1.22.0.0
InformationalVersion: r22
```

`AssemblyVersion` changes only when the major version changes. `FileVersion` is
the authoritative canonical release version. Runtime version displays derive the
`rN` label from `AssemblyFileVersionAttribute` and validate it against
`AssemblyInformationalVersionAttribute`; the revision is not duplicated in C#
source code.

The patch and build components are reserved and currently remain zero. Major is
normally 1 and serves primarily as a human-readable signal for a compatibility
break or exceptionally large feature. Increasing major also requires increasing
revision, but does not reset revision.

## Release identity and Git

The release revision is manually maintained in `src/Directory.Build.props`. It is not
tightly coupled to a branch, commit, tag, or working-tree state. Ordinary builds
from different commits can therefore report the same revision while development
for that release is in progress.

Git commit identifiers are deliberately excluded from compiled metadata.
`IncludeSourceRevisionInInformationalVersion` remains disabled, so a commit does
not alter the binary solely by changing its source revision. Release traceability
is maintained outside the binary through Git tags, release records, and published
artifact hashes.

Once an artifact is formally published for a revision, that release is immutable.
Any later release uses the next revision even if the change is small or confined
to an internal library. Because `OpExec.OnePassword` and `OpExec.SshAgent` are
embedded into the distributed standalone executable, changing either library
changes the OpExec release artifact and therefore advances the global revision.

The local `make package` workflow derives versioned archive names from
`ProductRevision`. The guarded `make release` workflow publishes those official
binary assets under the corresponding `rN` Git tag and GitHub Release. It
refuses to replace an existing tag or release and requires the release commit to
be the current `origin/main` commit.

## Explicit development labels

An optional development label can distinguish a deliberately identified test,
development, beta, or diagnostic build without changing the canonical revision.
Set the MSBuild `ProductBuildLabel` property explicitly:

```shell
dotnet build -p:ProductBuildLabel=test1
dotnet publish -p:ProductBuildLabel=beta1
dotnet build -p:ProductBuildLabel=abc1234
```

For `ProductRevision` 11 and label `test1`, the metadata is:

```text
Version:              1.11.0-test1
AssemblyVersion:      1.0.0.0
FileVersion:          1.11.0.0
InformationalVersion: r11-test1
Runtime display:      opexec r11-test1
```

The label defaults to empty and is never inferred automatically from Git. The
numeric file version continues to identify canonical revision 11; the optional
informational suffix identifies only the particular development build.

## Source-file headers

The revision in a major source file's comment header records the release in which
that file was last materially modified. Unchanged files retain their existing
header revisions; they are not mechanically rewritten for every global release.
