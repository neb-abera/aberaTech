# One node image for the client build, its tests, its lint pass and the dev
# server. Declared before the first FROM deliberately: only a global build
# argument may be used in a FROM instruction, and an ARG written after the first
# FROM belongs to that stage alone and expands to nothing here.
ARG NODE_IMAGE=node:22

# Base stage for .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Debugging: Print the value of the IN_DOCKER argument
RUN echo "nebdebug IN_DOCKER is set to: $IN_DOCKER"

# Base stage for client (React app)
#
# npm ci rather than npm install, here and in every stage below: the lockfile is
# committed, so the build should install exactly what it describes and fail
# loudly when it and package.json disagree, rather than quietly resolving
# something new on each build.
FROM ${NODE_IMAGE} AS clientbase
WORKDIR /client

# Install dependencies for the React app
COPY ["aberatech.client/package.json", "aberatech.client/package-lock.json", "./"]
RUN npm ci

# Client build stage
FROM clientbase AS clientbuild
COPY ["aberatech.client/", "./"]

# Build the React app
RUN IN_DOCKER=true npm run build

# Debugging: Validate that the dist folder is built
# List out the contents of the /client/dist folder to ensure it's built correctly
RUN echo "Contents of /client/dist (to verify React assets exist):" && \
    ls -l /client/dist && \
    echo "Contents of /client/dist/assets:" && \
    ls -l /client/dist/assets

# Client development server.
#
# Holds the dependency tree and nothing else: compose.yaml bind mounts the
# source over /client at run time, so edits on the host reload here. The
# anonymous volume declared there keeps this image's node_modules from being
# hidden by that mount.
#
# IN_DOCKER=true makes vite.config.ts skip generating an HTTPS certificate with
# dotnet dev-certs, which has no dotnet to run and no purpose in a container.
# --host 0.0.0.0 is required: vite binds to localhost by default, which inside a
# container is not reachable from the host.
FROM clientbase AS clientdev
ENV IN_DOCKER=true
EXPOSE 3000
CMD ["npx", "vite", "--host", "0.0.0.0", "--port", "3000"]

# Client test stage. A leaf: nothing downstream copies from it, so the
# production image build never pays for it.
#
#   docker build --target clienttest -f aberaTech.Server/Dockerfile .
#   make test
FROM clientbase AS clienttest
COPY ["aberatech.client/", "./"]
RUN npx tsc -b && npm run test

# Lint and format tooling. eslint, typescript-eslint and prettier live in the
# root package rather than the client one, and the config files they read sit
# beside it, so this stage reproduces the repository root rather than just the
# client directory. The build context is the repository root, so all of it is
# reachable.
#
# Split from the stage below on purpose: `make fmt` has to be able to start a
# container and rewrite files even when the tree does not currently pass, which
# it could not do if installing the tooling and checking the code were one step.
FROM ${NODE_IMAGE} AS clienttools
WORKDIR /repo
COPY ["package.json", "package-lock.json", "./"]
RUN npm ci

# The style gate. A leaf, like clienttest.
#
#   docker build --target clientlint -f aberaTech.Server/Dockerfile .
#   make lint
FROM clienttools AS clientlint
COPY . .
RUN npx eslint . && npx prettier --check .

# .NET build stage.
#
# Project files are copied before source so that editing a .cs file does not
# invalidate the restore layer. Both projects are copied at their real paths
# rather than flattened into /src: the server references aberaTech.Scheduling by
# relative path, and a flattened layout silently resolves that reference to
# nothing.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

WORKDIR /src

COPY ["aberaTech.Server/aberaTech.Server.csproj", "aberaTech.Server/"]
COPY ["aberaTech.Scheduling/aberaTech.Scheduling.csproj", "aberaTech.Scheduling/"]
RUN dotnet restore "aberaTech.Server/aberaTech.Server.csproj" -p:SkipClientProject=true

COPY aberaTech.Server ./aberaTech.Server
COPY aberaTech.Scheduling ./aberaTech.Scheduling
RUN dotnet build "aberaTech.Server/aberaTech.Server.csproj" \
    -c $BUILD_CONFIGURATION -o /app/build -p:SkipClientProject=true --no-restore

# The scheduling unit tests. A leaf, like clienttest: the production image never
# pays for it.
#
# They cover the daylight saving arithmetic, the queue projection and the
# delivery retry schedule — the three things that are wrong in every scheduling
# tool that has ever sent somebody to the wrong meeting at the wrong time.
#
#   docker build --target servertest -f aberaTech.Server/Dockerfile .
#   make servertest
FROM build AS servertest
ARG BUILD_CONFIGURATION=Release
COPY ["aberaTech.Server.Tests/aberaTech.Server.Tests.csproj", "aberaTech.Server.Tests/"]
RUN dotnet restore "aberaTech.Server.Tests/aberaTech.Server.Tests.csproj"
COPY aberaTech.Server.Tests ./aberaTech.Server.Tests
RUN dotnet test "aberaTech.Server.Tests/aberaTech.Server.Tests.csproj" -c $BUILD_CONFIGURATION --no-restore

# Publish .NET server
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "aberaTech.Server/aberaTech.Server.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish -p:SkipClientProject=true /p:UseAppHost=false

# Final production-ready stage
FROM base AS final
WORKDIR /app

ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

# Copy the .NET publish output
COPY --from=publish /app/publish .

# Ensure wwwroot folder exists
RUN mkdir -p /app/wwwroot

# Copy the React app build output into the server's wwwroot folder
COPY --from=clientbuild /client/dist /app/wwwroot

# Debugging: Validate copied React static files in the final image
RUN echo "Contents of /app/wwwroot (to verify final static files):" && \
    ls -l /app/wwwroot && \
    echo "Contents of /app/wwwroot/assets (if assets exist):" && \
    ls -l /app/wwwroot/assets || echo "No assets folder found in /app/wwwroot"

# Set the entry point for the .NET backend
ENTRYPOINT ["dotnet", "aberaTech.Server.dll"]
