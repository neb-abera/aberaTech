# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Base image for the client
FROM node:22.10.2 AS clientbase
WORKDIR /client
COPY ["aberatech.client/package.json", "aberatech.client/package-lock.json", "./"]
RUN npm install

# This stage is used to build the client project
FROM clientbase AS clientbuild
COPY ["aberatech.client/", "./"]
RUN npm run build

# This stage is used to build the server project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["aberaTech.Server/aberaTech.Server.csproj", "aberaTech.Server/"]
RUN dotnet restore "./aberaTech.Server/aberaTech.Server.csproj"
COPY aberaTech.Server .
WORKDIR "/src/aberaTech.Server"
RUN dotnet build "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the server project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./aberaTech.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage to serve the client and run the server
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/wwwroot
COPY --from=clientbuild /client/dist /app/wwwroot
ENTRYPOINT ["dotnet", "aberaTech.Server.dll"]