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
FROM node:22 AS clientbase
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