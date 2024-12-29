# Base stage for .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Pass the build argument into this stage
ARG IN_DOCKER
# Make the ARG available as an environment variable
ENV IN_DOCKER=$IN_DOCKER
RUN echo "nebdebug IN_DOCKER is set to: $IN_DOCKER"

USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Base stage for the client (React app)
FROM node:22 AS clientbase
WORKDIR /client
COPY ["aberatech.client/package.json", "aberatech.client/package-lock.json", "./"]
RUN npm install

# Client build stage
FROM clientbase AS clientbuild
COPY ["aberatech.client/", "./"]
RUN IN_DOCKER=true npm run build

# .NET server build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Pass IN_DOCKER argument to this stage if needed
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

# Restore .NET dependencies
COPY ["aberaTech.Server/aberaTech.Server.csproj", "./"]
RUN dotnet restore "./aberaTech.Server.csproj"

# Copy the .NET source code and build it
COPY aberaTech.Server ./aberaTech.Server
WORKDIR /src/aberaTech.Server
RUN dotnet build "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish .NET server
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage (production-ready)
FROM base AS final
WORKDIR /app

# Pass IN_DOCKER argument to the final stage if needed
ARG IN_DOCKER
ENV IN_DOCKER=$IN_DOCKER

# Copy server files from the publish build stage
COPY --from=publish /app/publish .

# Ensure wwwroot folder exists
RUN mkdir -p /app/wwwroot

# Copy the frontend files from the client build stage
COPY --from=clientbuild /client/dist /app/wwwroot

# Provide the entry point for the .NET backend
ENTRYPOINT ["dotnet", "aberaTech.Server.dll"]