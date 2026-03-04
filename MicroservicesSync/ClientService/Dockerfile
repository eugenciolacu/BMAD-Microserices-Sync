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
RUN dotnet restore ClientService/ClientService.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish ClientService/ClientService.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Create directories for SQLite DB and logs (named volumes will mount here)
USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app/logs
RUN mkdir -p /app/SqLiteDatabase && chown -R $APP_UID:$APP_UID /app/SqLiteDatabase
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClientService.dll"]
