# syntax=docker/dockerfile:1

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401-noble
FROM ${DOTNET_SDK_IMAGE} AS restore

WORKDIR /source

COPY src/Directory.Build.props src/OpExec.slnx src/
COPY src/OpExec/OpExec.csproj src/OpExec/
COPY src/OpExec.OnePassword/OpExec.OnePassword.csproj src/OpExec.OnePassword/
COPY src/OpExec.SshAgent/OpExec.SshAgent.csproj src/OpExec.SshAgent/
COPY src/OpExec.Tests/OpExec.Tests.csproj src/OpExec.Tests/
COPY src/OpExec.OnePassword.Tests/OpExec.OnePassword.Tests.csproj src/OpExec.OnePassword.Tests/
COPY src/OpExec.SshAgent.Tests/OpExec.SshAgent.Tests.csproj src/OpExec.SshAgent.Tests/
RUN dotnet restore src/OpExec.slnx && \
    dotnet restore src/OpExec/OpExec.csproj --runtime linux-x64

FROM restore AS source
COPY . .

FROM source AS build
ARG CONFIGURATION=Release
RUN dotnet build src/OpExec.slnx \
    --configuration ${CONFIGURATION} \
    --artifacts-path /artifacts

FROM scratch AS build-artifact
COPY --from=build /artifacts/ /

FROM source AS test
ARG CONFIGURATION=Release
RUN dotnet test src/OpExec.slnx \
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
