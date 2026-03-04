# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY MicrosericesSync.sln ./
COPY ServerService/ServerService.csproj ServerService/
COPY ClientService/ClientService.csproj ClientService/
COPY Sync.Domain/Sync.Domain.csproj Sync.Domain/
COPY Sync.Application/Sync.Application.csproj Sync.Application/
COPY Sync.Infrastructure/Sync.Infrastructure.csproj Sync.Infrastructure/

# Restore NuGet packages
RUN dotnet restore ServerService/ServerService.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish ServerService/ServerService.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app/logs
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServerService.dll"]
