#!/bin/sh
# SPDX-FileCopyrightText: © 2026 Andrew J. Moore
# SPDX-FileContributor: Andrew J. Moore
# SPDX-License-Identifier: MIT
#
# =============================================================================
# Script:       scripts/package.sh
# Author:       Andrew J. Moore
# Revised:      2026-09-21
# Revision:     r16
# Source:       https://github.com/bobapplemac/opexec
#
# Purpose:
#   Tests and publishes a fresh Linux x64 executable, then creates a versioned
#   local archive and SHA-256 checksum without publishing them externally.
#
# Comments:
#   Package identity is derived from ProductRevision in Directory.Build.props.
#   Existing local outputs are replaced only after new package files are complete.
#
# Dependencies:
#   sed, tar, sha256sum, mktemp, and standard Unix file utilities.
#   Debian: apt install sed tar coreutils
#   Build dependencies are documented by scripts/build.sh.
#
# Environment:
#   BUILD_BACKEND    - auto (default), dotnet, or docker; passed to build.sh.
#   DOTNET           - Native dotnet command; default: dotnet.
#   DOCKER           - Docker command; default: docker.
#   CONFIGURATION    - MSBuild configuration; default: Release.
#   PUBLISH_PROFILE  - Publish profile name; default: linux-x64.
#   DOTNET_SDK_IMAGE - Docker SDK image used by build.sh.
#
# Usage:
#   sh scripts/package.sh
#
# Arguments:
#   None. Unknown positional arguments are not supported.
# =============================================================================

set -eu

case "$0" in
    /*) script_path=$0 ;;
    *) script_path=$PWD/$0 ;;
esac

script_dir=$(CDPATH= cd -- "${script_path%/*}" && pwd)
repo_dir=$(CDPATH= cd -- "$script_dir/.." && pwd)
build_script=$script_dir/build.sh
version_props=$repo_dir/src/Directory.Build.props
publish_dir=$repo_dir/artifacts/publish/linux-x64
binary=$publish_dir/opexec
release_dir=$repo_dir/artifacts/release

cd "$repo_dir"

fail() {
    echo "Error: $*" >&2
    exit 1
}

[ "$#" -eq 0 ] || fail "package.sh does not accept positional arguments."

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        fail "required command not found: $1"
    fi
}

read_msbuild_property() {
    property=$1
    sed -n "s|.*<$property>\([^<]*\)</$property>.*|\1|p" "$version_props" |
        sed -n '1p'
}

require_command sed
require_command tar
require_command sha256sum
require_command mktemp

[ "$(uname -s)" = Linux ] || fail "release packages are currently produced only on Linux."
[ -f "$version_props" ] || fail "version properties not found: $version_props"

product_revision=$(read_msbuild_property ProductRevision)
case "$product_revision" in
    ''|*[!0-9]*) fail "ProductRevision must be a non-negative integer." ;;
esac

release_tag=r$product_revision
asset_name=opexec-$release_tag-linux-x64.tar.gz
checksum_name=$asset_name.sha256
asset_path=$release_dir/$asset_name
checksum_path=$release_dir/$checksum_name

"$build_script" test
"$build_script" publish

[ -f "$binary" ] || fail "published binary not found: $binary"

mkdir -p "$release_dir"

temporary_dir=$(mktemp -d)
cleanup() {
    rm -rf "$temporary_dir"
}
trap cleanup EXIT INT TERM

cp "$binary" "$temporary_dir/opexec"
chmod 0755 "$temporary_dir/opexec"
tar -C "$temporary_dir" -czf "$temporary_dir/$asset_name" opexec
(
    cd "$temporary_dir"
    sha256sum "$asset_name" >"$checksum_name"
)

mv "$temporary_dir/$asset_name" "$asset_path"
mv "$temporary_dir/$checksum_name" "$checksum_path"

trap - EXIT INT TERM
cleanup

echo "Packaged $asset_path"
echo "Checksum $checksum_path"
