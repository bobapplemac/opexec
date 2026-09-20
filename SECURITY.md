# Security

## Reporting a vulnerability

Email [Andrew J. Moore](mailto:andrew@forevermoore.net) privately with a description
of the issue, the OpExec revision, operating system, expected and actual behavior,
and reproduction steps using synthetic credentials where possible. Please do not
post exploit details in a public issue before coordinating disclosure.

Do not send real private keys, passwords, session tokens, or 1Password vault
contents. Redact sensitive information from logs and examples.

OpExec is a personal project with no guaranteed response or remediation timeline.
This repository starts with the r12 source snapshot; there are no maintained older
public release branches. Include the exact source revision when reporting a bug.

## Security scope

See [security hardening boundaries](docs/security-hardening.md) for the implemented
controls and their limits. OpExec relies on the operating system, the external
1Password CLI, and the commands you launch. Only run trusted commands inside an
authenticated scope: they receive its authentication environment and SSH-agent
access. The scope does not isolate credentials from the current user or a
privileged attacker.

The repository includes intentionally public test private keys and a deterministic
test seed, documented in [tests/README.md](tests/README.md). They must never be
authorized on real systems. Reports of exposed credentials elsewhere are welcome;
do not assume other keys are fixtures.
