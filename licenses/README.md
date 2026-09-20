# Bundled license notices

This directory contains the license texts and notices for third-party software
included in the self-contained `opexec` executable:

- `Mono.Options-6.12.0.148.txt` — Mono.Options 6.12.0.148
- `CliWrap-3.10.5.txt` — CliWrap 3.10.5
- `BouncyCastle.Cryptography-2.7.0.txt` — BouncyCastle.Cryptography 2.7.0
- `dotnet-runtime-10.0.12.txt` — .NET Runtime 10.0.12
- `dotnet-runtime-10.0.12-THIRD-PARTY-NOTICES.txt` — complete .NET Runtime
  10.0.12 third-party notices

The repository's own license is maintained at [`../LICENSE.txt`](../LICENSE.txt).
All of these texts are embedded in the published executable and can be printed
with `opexec --licenses`, `opshell --licenses`, `opssh --licenses`, or
`opssh --agent --licenses`.

The Mono.Options notice reflects the MIT license and copyright notices in the
bundled Mono.Options source. Notices for other components of the wider Mono
distribution are not included because those components are not part of the
Mono.Options package embedded by OpExec.

The external `op` and `ssh` programs invoked by OpExec are not distributed in
the executable and therefore are not represented here.
