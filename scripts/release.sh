#!/bin/sh

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

require_clean_tree() {
    if [ -n "$(git status --porcelain)" ]; then
        echo "Error: releases require a clean Git working tree." >&2
        echo >&2
        git status --short >&2
        exit 1
    fi
}

require_command git
require_command gh
require_command sed
require_command tar
require_command sha256sum

[ "$(uname -s)" = Linux ] || fail "GitHub releases are currently produced only on Linux."
[ -f "$version_props" ] || fail "version properties not found: $version_props"

git rev-parse --is-inside-work-tree >/dev/null 2>&1 ||
    fail "$repo_dir is not a Git repository."
git remote get-url origin >/dev/null 2>&1 || fail "Git remote 'origin' is not configured."
require_clean_tree

if ! gh auth status >/dev/null 2>&1; then
    fail "GitHub CLI is not authenticated; run 'gh auth login' or set GH_TOKEN."
fi

echo "Fetching origin refs and tags..."
git fetch --quiet --prune --tags origin \
    '+refs/heads/*:refs/remotes/origin/*'

git_commit=$(git rev-parse HEAD)
git_short=$(git rev-parse --short=7 HEAD)
origin_main=$(git rev-parse origin/main 2>/dev/null) ||
    fail "origin/main is not available."

if [ "$git_commit" != "$origin_main" ]; then
    echo "Error: releases must be created from the current origin/main commit." >&2
    echo >&2
    echo "  HEAD:        $git_short" >&2
    echo "  origin/main: $(git rev-parse --short=7 origin/main)" >&2
    echo >&2
    echo "Push or update the repository, then retry." >&2
    exit 1
fi

product_revision=$(read_msbuild_property ProductRevision)
case "$product_revision" in
    ''|*[!0-9]*) fail "ProductRevision must be a non-negative integer." ;;
esac

release_tag=r$product_revision
release_title="OpExec $release_tag"
asset_name="opexec-$release_tag-linux-x64.tar.gz"
checksum_name="$asset_name.sha256"
asset_path=$release_dir/$asset_name
checksum_path=$release_dir/$checksum_name

github_repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner) ||
    fail "unable to resolve the GitHub repository."
[ -n "$github_repo" ] || fail "GitHub returned an empty repository name."

if git show-ref --verify --quiet "refs/tags/$release_tag"; then
    fail "Git tag $release_tag already exists; published revisions are immutable."
fi

if gh release view "$release_tag" --repo "$github_repo" >/dev/null 2>&1; then
    fail "GitHub Release $release_tag already exists; published revisions are immutable."
fi

echo
echo "Preparing $release_title from $github_repo at $git_short..."
echo

"$build_script" test
"$build_script" publish

[ -f "$binary" ] || fail "published binary not found: $binary"

mkdir -p "$release_dir"
rm -f "$asset_path" "$checksum_path"

temporary_dir=$(mktemp -d)
cleanup() {
    rm -rf "$temporary_dir"
}
trap cleanup EXIT INT TERM

cp "$binary" "$temporary_dir/opexec"
chmod 0755 "$temporary_dir/opexec"
tar -C "$temporary_dir" -czf "$asset_path" opexec
(
    cd "$release_dir"
    sha256sum "$asset_name" >"$checksum_name"
)

trap - EXIT INT TERM
cleanup

echo
echo "Creating GitHub Release $release_tag..."
echo "  Repository: $github_repo"
echo "  Commit:     $git_short"
echo "  Asset:      $asset_name"
echo "  Checksum:   $checksum_name"
echo

gh release create "$release_tag" \
    "$asset_path" \
    "$checksum_path" \
    --repo "$github_repo" \
    --target "$git_commit" \
    --title "$release_title" \
    --generate-notes \
    --latest

echo
echo "Published GitHub Release $release_tag."
