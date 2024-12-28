# Base image for the client
FROM node:22 AS clientbase
WORKDIR /aberatech.client

# Copy package files and install dependencies
COPY ["aberatech.client/package.json", "aberatech.client/package-lock.json", "./"]
RUN npm install

# This stage is used to build the client project
FROM clientbase AS clientbuild
WORKDIR /aberatech.client

# Copy the client source files into the container
COPY ["aberatech.client/", "./"]

# Add environment variable to disable HTTPS in Vite
ENV IN_DOCKER=true

# Build the client
RUN npm run build

# This stage is used to build the server project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /aberaTech.Server

# Restore .NET dependencies
COPY ["aberaTech.Server/aberaTech.Server.csproj", "/aberaTech.Server/"]
RUN dotnet restore "./aberaTech.Server.csproj"

# Build the .NET backend
COPY aberaTech.Server /aberaTech.Server
RUN dotnet build "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish server files for deployment
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage: Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy .NET publish output into runtime container
COPY --from=publish /app/publish .

# Create the folder for the client build
RUN mkdir -p /app/wwwroot

# Copy static client build output from Vite
COPY --from=clientbuild /aberatech.client/dist /app/wwwroot

# Provide the entry point for the .NET backend
ENTRYPOINT ["dotnet", "aberaTech.Server.dll"]