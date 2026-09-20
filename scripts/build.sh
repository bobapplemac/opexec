#!/bin/sh

set -eu

case "$0" in
    /*) script_path=$0 ;;
    *) script_path=$PWD/$0 ;;
esac

script_dir=$(CDPATH= cd -- "${script_path%/*}" && pwd)
repo_dir=$(CDPATH= cd -- "$script_dir/.." && pwd)
cd "$repo_dir"

action=${1:-publish}
backend=${BUILD_BACKEND:-auto}
dotnet_command=${DOTNET:-dotnet}
docker_command=${DOCKER:-docker}
configuration=${CONFIGURATION:-Release}
publish_profile=${PUBLISH_PROFILE:-linux-x64}
sdk_image=${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0.401-noble}
artifacts_dir=$repo_dir/artifacts
publish_dir=$artifacts_dir/publish/linux-x64
binary=$publish_dir/opexec

clean_outputs() {
    rm -rf \
        "$artifacts_dir" \
        src/OpExec/bin src/OpExec/obj \
        src/OpExec.OnePassword/bin src/OpExec.OnePassword/obj \
        src/OpExec.SshAgent/bin src/OpExec.SshAgent/obj \
        tests/OpExec.Tests/bin tests/OpExec.Tests/obj \
        tests/OpExec.OnePassword.Tests/bin tests/OpExec.OnePassword.Tests/obj \
        tests/OpExec.SshAgent.Tests/bin tests/OpExec.SshAgent.Tests/obj
}

if [ "$action" = clean ]; then
    clean_outputs
    exit 0
fi

has_dotnet_10_sdk() {
    command -v "$dotnet_command" >/dev/null 2>&1 &&
        "$dotnet_command" --list-sdks 2>/dev/null | grep -q '^10\.'
}

has_docker() {
    command -v "$docker_command" >/dev/null 2>&1
}

case "$backend" in
    auto)
        if has_dotnet_10_sdk; then
            backend=dotnet
        elif has_docker; then
            backend=docker
        else
            echo "A .NET 10 SDK or Docker is required." >&2
            exit 1
        fi
        ;;
    dotnet)
        if ! has_dotnet_10_sdk; then
            echo "BUILD_BACKEND=dotnet requires a .NET 10 SDK." >&2
            exit 1
        fi
        ;;
    docker)
        if ! has_docker; then
            echo "BUILD_BACKEND=docker requires Docker." >&2
            exit 1
        fi
        ;;
    *)
        echo "Unknown BUILD_BACKEND '$backend'; expected auto, dotnet, or docker." >&2
        exit 1
        ;;
esac

run_dotnet() {
    case "$action" in
        restore)
            "$dotnet_command" restore OpExec.slnx
            ;;
        build)
            "$dotnet_command" build OpExec.slnx \
                --configuration "$configuration" \
                --artifacts-path "$artifacts_dir"
            ;;
        test)
            "$dotnet_command" test OpExec.slnx \
                --configuration "$configuration" \
                --artifacts-path "$artifacts_dir"
            ;;
        publish)
            "$dotnet_command" publish src/OpExec/OpExec.csproj \
                --configuration "$configuration" \
                -p:PublishProfile="$publish_profile"
            ;;
        *)
            echo "Unknown build action '$action'." >&2
            exit 1
            ;;
    esac
}

run_docker() {
    common_args="--platform linux/amd64 --build-arg DOTNET_SDK_IMAGE=$sdk_image --build-arg CONFIGURATION=$configuration"

    case "$action" in
        restore)
            # Deliberate word splitting keeps the shared Docker arguments separate.
            # shellcheck disable=SC2086
            "$docker_command" build $common_args \
                --target restore \
                --file scripts/Dockerfile .
            ;;
        build)
            mkdir -p "$artifacts_dir"
            # shellcheck disable=SC2086
            "$docker_command" build $common_args \
                --target build-artifact \
                --output "type=local,dest=$artifacts_dir" \
                --file scripts/Dockerfile .
            ;;
        test)
            # shellcheck disable=SC2086
            "$docker_command" build $common_args \
                --target test \
                --file scripts/Dockerfile .
            ;;
        publish)
            mkdir -p "$publish_dir"
            rm -f "$binary"
            # shellcheck disable=SC2086
            "$docker_command" build $common_args \
                --target publish-artifact \
                --output "type=local,dest=$publish_dir" \
                --file scripts/Dockerfile .
            ;;
        *)
            echo "Unknown build action '$action'." >&2
            exit 1
            ;;
    esac
}

echo "Using $backend build backend."

if [ "$backend" = dotnet ]; then
    run_dotnet
else
    run_docker
fi

if [ "$action" = publish ]; then
    if [ ! -f "$binary" ]; then
        echo "Expected binary was not produced at $binary" >&2
        exit 1
    fi

    echo "Published $binary"
fi
