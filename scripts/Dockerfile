# syntax=docker/dockerfile:1

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401-noble
FROM ${DOTNET_SDK_IMAGE} AS restore

WORKDIR /source

COPY Directory.Build.props OpExec.slnx ./
COPY src/OpExec/OpExec.csproj src/OpExec/
COPY src/OpExec.OnePassword/OpExec.OnePassword.csproj src/OpExec.OnePassword/
COPY src/OpExec.SshAgent/OpExec.SshAgent.csproj src/OpExec.SshAgent/
COPY tests/OpExec.Tests/OpExec.Tests.csproj tests/OpExec.Tests/
COPY tests/OpExec.OnePassword.Tests/OpExec.OnePassword.Tests.csproj tests/OpExec.OnePassword.Tests/
COPY tests/OpExec.SshAgent.Tests/OpExec.SshAgent.Tests.csproj tests/OpExec.SshAgent.Tests/
RUN dotnet restore OpExec.slnx && \
    dotnet restore src/OpExec/OpExec.csproj --runtime linux-x64

FROM restore AS source
COPY . .

FROM source AS build
ARG CONFIGURATION=Release
RUN dotnet build OpExec.slnx \
    --configuration ${CONFIGURATION} \
    --artifacts-path /artifacts

FROM scratch AS build-artifact
COPY --from=build /artifacts/ /

FROM source AS test
ARG CONFIGURATION=Release
RUN dotnet test OpExec.slnx \
    --configuration ${CONFIGURATION} \
    --artifacts-path /artifacts

FROM source AS publish
ARG CONFIGURATION=Release
RUN dotnet publish src/OpExec/OpExec.csproj \
    --configuration ${CONFIGURATION} \
    -p:PublishProfile=linux-x64 \
    --no-restore

FROM scratch AS publish-artifact
COPY --from=publish /source/artifacts/publish/linux-x64/opexec /opexec
