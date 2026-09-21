# SPDX-FileCopyrightText: © 2026 Andrew J. Moore
# SPDX-FileContributor: Andrew J. Moore
# SPDX-License-Identifier: MIT
#
# ------------------------------------------------------------------------------------------
# File:        Makefile
# Revision:    r16
# Modified:    2026-09-21
# Author:      Andrew J. Moore
# License:     MIT License
# Source:      https://github.com/bobapplemac/opexec
# Description: Provides the stable Make front end for local build, test, publish, package,
#              installation, cleanup, and explicit external release workflows.
# ------------------------------------------------------------------------------------------

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
PACKAGE_SCRIPT := scripts/package.sh
RELEASE_SCRIPT := scripts/release.sh
BINARY := artifacts/publish/linux-x64/opexec

.PHONY: build clean help install package publish release test uninstall

build:
	$(SHELL) "$(BUILD_SCRIPT)" build

test:
	$(SHELL) "$(BUILD_SCRIPT)" test

publish:
	$(SHELL) "$(BUILD_SCRIPT)" publish

package:
	$(SHELL) "$(PACKAGE_SCRIPT)"

release:
	$(SHELL) "$(RELEASE_SCRIPT)"

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
	@echo "  make package         Create a local versioned archive and checksum in artifacts/release"
	@echo "  make install         Install an existing published binary system-wide"
	@echo "  make uninstall       Uninstall the system-wide command"
	@echo "  make clean           Remove artifacts plus project-local bin/obj outputs"
	@echo "  make help            Show this help"
	@echo "  make release         Publish official versioned assets to GitHub (external)"
	@echo
	@echo "Build, test, publish, and package prefer a local .NET 10 SDK and fall back to Docker."
	@echo "Set BUILD_BACKEND=dotnet or BUILD_BACKEND=docker to override automatic selection."
	@echo "Examples: make publish BUILD_BACKEND=dotnet"
	@echo "          make package BUILD_BACKEND=docker"
	@echo "Publish and package are local operations; neither uploads a GitHub Release."
	@echo "Releasing requires GitHub CLI authentication from gh auth login or GH_TOKEN."
	@echo "Override tools/settings with DOTNET, DOCKER, CONFIGURATION, or PUBLISH_PROFILE."
	@echo "For a system install, use: make && sudo make install"
