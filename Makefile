.DEFAULT_GOAL := publish

SHELL := /bin/sh

BUILD_BACKEND ?= auto
DOTNET ?= dotnet
DOCKER ?= docker
CONFIGURATION ?= Release
PUBLISH_PROFILE ?= linux-x64
DOTNET_SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:10.0.401-noble

export BUILD_BACKEND DOTNET DOCKER CONFIGURATION PUBLISH_PROFILE DOTNET_SDK_IMAGE

BUILD_SCRIPT := scripts/build.sh
BINARY := artifacts/publish/linux-x64/opexec

.PHONY: all build clean docker-build docker-publish docker-test help install \
	native-build native-publish native-test publish restore test uninstall

all: publish

restore:
	$(SHELL) "$(BUILD_SCRIPT)" restore

build:
	$(SHELL) "$(BUILD_SCRIPT)" build

test:
	$(SHELL) "$(BUILD_SCRIPT)" test

publish:
	$(SHELL) "$(BUILD_SCRIPT)" publish

native-build:
	BUILD_BACKEND=dotnet $(SHELL) "$(BUILD_SCRIPT)" build

native-test:
	BUILD_BACKEND=dotnet $(SHELL) "$(BUILD_SCRIPT)" test

native-publish:
	BUILD_BACKEND=dotnet $(SHELL) "$(BUILD_SCRIPT)" publish

docker-build:
	BUILD_BACKEND=docker $(SHELL) "$(BUILD_SCRIPT)" build

docker-test:
	BUILD_BACKEND=docker $(SHELL) "$(BUILD_SCRIPT)" test

docker-publish:
	BUILD_BACKEND=docker $(SHELL) "$(BUILD_SCRIPT)" publish

install:
	@test "$$(uname -s)" = Linux || { echo "System installation is supported only on Linux." >&2; exit 1; }
	@test -f "$(BINARY)" || { echo "$(BINARY) does not exist; run make first." >&2; exit 1; }
	"$(BINARY)" --install

uninstall:
	@test "$$(uname -s)" = Linux || { echo "System uninstallation is supported only on Linux." >&2; exit 1; }
	opexec --uninstall

clean:
	$(SHELL) "$(BUILD_SCRIPT)" clean

help:
	@echo "OpExec build targets"
	@echo
	@echo "  make                 Publish the Linux x64 single-file executable (default)"
	@echo "  make build           Compile the solution into artifacts/bin"
	@echo "  make test            Build and run the test suite"
	@echo "  make publish         Create the local deployable binary in artifacts/publish"
	@echo "  make install         Install an existing published binary system-wide"
	@echo "  make uninstall       Uninstall the system-wide command"
	@echo "  make clean           Remove artifacts plus project-local bin/obj outputs"
	@echo "  make help            Show this help"
	@echo
	@echo "Build, test, and publish prefer a local .NET 10 SDK and fall back to Docker."
	@echo "Set BUILD_BACKEND=dotnet or BUILD_BACKEND=docker to override automatic selection."
	@echo "Examples: make publish BUILD_BACKEND=dotnet"
	@echo "          make publish BUILD_BACKEND=docker"
	@echo "The native-publish and docker-publish convenience targets are equivalent shortcuts."
	@echo "Publishing creates a local artifact; it does not upload a GitHub Release."
	@echo "Override tools/settings with DOTNET, DOCKER, CONFIGURATION, or PUBLISH_PROFILE."
	@echo "For a system install, use: make && sudo make install"
