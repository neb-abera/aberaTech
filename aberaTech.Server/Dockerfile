# Base stage for .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Debugging: Print the value of the IN_DOCKER argument
RUN echo "nebdebug IN_DOCKER is set to: $IN_DOCKER"

# One node image for both the client build and the client tests. Sharing the
# argument is the point: if the tests ran on a different node than the build,
# a green test would stop meaning anything about the artifact.
ARG NODE_IMAGE=node:22

# Base stage for client (React app)
FROM ${NODE_IMAGE} AS clientbase
WORKDIR /client

# Install dependencies for the React app
COPY ["aberatech.client/package.json", "aberatech.client/package-lock.json", "./"]
RUN npm install

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

# Client test stage. A leaf: nothing downstream copies from it, so the
# production image build never pays for it. Run it deliberately:
#
#   docker build --target clienttest -f aberaTech.Server/Dockerfile .
#
# npm ci rather than npm install, because the whole reason to test in a
# container is reproducibility, and npm ci installs exactly what
# package-lock.json describes and fails loudly when the lockfile and
# package.json disagree.
#
# Type checking and unit tests only. eslint and prettier read their config from
# the repository root, which is outside this build context, so they run in CI
# rather than here; see .github/workflows/client-checks.yml.
FROM ${NODE_IMAGE} AS clienttest
WORKDIR /client
COPY ["aberatech.client/package.json", "aberatech.client/package-lock.json", "./"]
RUN npm ci
COPY ["aberatech.client/", "./"]
RUN npx tsc -b && npm run test

# .NET build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

WORKDIR /src

# Restore .NET dependencies
COPY ["aberaTech.Server/aberaTech.Server.csproj", "./"]
RUN dotnet restore "./aberaTech.Server.csproj"

# Copy and build .NET source code
COPY aberaTech.Server ./aberaTech.Server
WORKDIR /src/aberaTech.Server
RUN dotnet build "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish .NET server
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

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