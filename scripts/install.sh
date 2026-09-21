#!/bin/sh
# SPDX-FileCopyrightText: © 2026 Andrew J. Moore
# SPDX-FileContributor: Andrew J. Moore
# SPDX-License-Identifier: MIT

# Downloads, verifies, and installs an OpExec GitHub release for Linux x64.
# With no argument, the latest release is selected. Pass an explicit rN tag to
# install a specific release, for example: scripts/install.sh r16

set -eu

repository_url=https://github.com/bobapplemac/opexec

fail() {
    echo "Error: $*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

validate_release_tag() {
    candidate=$1

    case "$candidate" in
        r[0-9]*) ;;
        *) fail "invalid release '$candidate'; expected a tag such as r16" ;;
    esac

    case "${candidate#r}" in
        ''|*[!0-9]*) fail "invalid release '$candidate'; expected a tag such as r16" ;;
    esac
}

[ "$#" -le 1 ] || fail "usage: install.sh [rN]"

case "${1:-}" in
    -h|--help)
        echo "usage: install.sh [rN]"
        echo
        echo "Downloads, verifies, and installs the latest OpExec release."
        echo "Pass a release tag such as r16 to install a specific version."
        exit 0
        ;;
esac

requested_release=${1:-}

if [ -n "$requested_release" ]; then
    validate_release_tag "$requested_release"
fi

require_command uname
require_command id
require_command curl
require_command sha256sum
require_command tar
require_command mktemp

[ "$(uname -s)" = Linux ] || fail "OpExec installation is supported only on Linux"

case "$(uname -m)" in
    x86_64|amd64) ;;
    *) fail "the published OpExec release supports only Linux x64" ;;
esac

if [ "$(id -u)" -eq 0 ]; then
    use_sudo=false
elif command -v sudo >/dev/null 2>&1; then
    use_sudo=true
else
    fail "system installation requires root privileges, but sudo is not installed"
fi

if [ -n "$requested_release" ]; then
    release_tag=$requested_release
else
    echo "Discovering the latest OpExec release..."
    latest_url=$(curl -fsSL -o /dev/null -w '%{url_effective}' \
        "$repository_url/releases/latest") ||
        fail "unable to discover the latest OpExec release"
    release_tag=${latest_url##*/}
    validate_release_tag "$release_tag"
fi

archive_name=opexec-$release_tag-linux-x64.tar.gz
checksum_name=$archive_name.sha256
download_url=$repository_url/releases/download/$release_tag
temporary_dir=$(mktemp -d)

cleanup() {
    rm -rf "$temporary_dir"
}

trap cleanup 0
trap 'exit 1' 1 2 15

echo "Downloading OpExec $release_tag..."
(
    cd "$temporary_dir"
    curl -fsSLO "$download_url/$archive_name" ||
        fail "unable to download $archive_name"
    curl -fsSLO "$download_url/$checksum_name" ||
        fail "unable to download $checksum_name"

    echo "Verifying $archive_name..."
    sha256sum -c "$checksum_name"

    archive_entries=$(tar -tzf "$archive_name")
    [ "$archive_entries" = opexec ] ||
        fail "$archive_name contains unexpected files"
    tar -xzf "$archive_name"
    chmod 0755 opexec

    echo "Installing OpExec $release_tag..."

    if [ "$use_sudo" = true ]; then
        sudo ./opexec --install
    else
        ./opexec --install
    fi
)
