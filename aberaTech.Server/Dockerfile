# One node image for the client build, its tests, its lint pass and the dev
# server. A named stage rather than an ARG deliberately: Dependabot cannot see
# an image behind ARG indirection, but it watches a literal FROM, and every
# consumer derives from this one stage so a bump moves them all together.
FROM node:26 AS nodebase

# Base stage for .NET runtime
# Chiseled: distroless-style, no shell, no package manager, non-root by
# default - and none of the ~30 unfixable distro CVEs the full image carries.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:0839314d08bb65da369135389a5d8291f75ace587fbb0488f469eb92c62eef68 AS base
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Debugging: Print the value of the IN_DOCKER argument

# Base stage for client (React app)
#
# npm ci rather than npm install, here and in every stage below: the lockfile is
# committed, so the build should install exactly what it describes and fail
# loudly when it and package.json disagree, rather than quietly resolving
# something new on each build.
FROM nodebase AS clientbase
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

# Lint and format tooling. Biome lives in the root package rather than the
# client one, and biome.json sits beside it, so this stage reproduces the
# repository root rather than just the client directory. The build context is the repository root, so all of it is
# reachable.
#
# Split from the stage below on purpose: `make fmt` has to be able to start a
# container and rewrite files even when the tree does not currently pass, which
# it could not do if installing the tooling and checking the code were one step.
FROM nodebase AS clienttools
WORKDIR /repo
COPY ["package.json", "package-lock.json", "./"]
RUN npm ci

# The style gate. A leaf, like clienttest.
#
#   docker build --target clientlint -f aberaTech.Server/Dockerfile .
#   make lint
FROM clienttools AS clientlint
COPY . .
RUN npx biome check .

# .NET build stage.
#
# Project files are copied before source so that editing a .cs file does not
# invalidate the restore layer. Both projects are copied at their real paths
# rather than flattened into /src: the server references aberaTech.Scheduling by
# relative path, and a flattened layout silently resolves that reference to
# nothing.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
RUN dotnet restore "aberaTech.Server.Tests/aberaTech.Server.Tests.csproj" -p:SkipClientProject=true
COPY aberaTech.Server.Tests ./aberaTech.Server.Tests
RUN dotnet test "aberaTech.Server.Tests/aberaTech.Server.Tests.csproj" -c $BUILD_CONFIGURATION --no-restore -p:SkipClientProject=true

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

# Copy the React app build output into the server's wwwroot folder (COPY
# creates the directory; chiseled images have no shell for RUN steps).
COPY --from=clientbuild /client/dist /app/wwwroot

# Drop privileges. The aspnet image ships an app user (APP_UID) for exactly
# this; port 8080 needs no root to bind.
USER $APP_UID

# Set the entry point for the .NET backend
ENTRYPOINT ["dotnet", "aberaTech.Server.dll"]
