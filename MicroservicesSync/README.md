# Microserices-Sync

Local development environment for multi-client sync experiments.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- A `.env` file in this folder containing `SA_PASSWORD=<YourStrong@Passw0rd>`

## Quick Start

```bash
# First run / after code changes
docker-compose up --build

# Subsequent runs (images already built)
docker-compose up

# Stop all containers
docker-compose down
```

## Verifying the Environment is Healthy

Once all containers are running, check each service's `/health` endpoint.
Every endpoint should return HTTP 200 with `{"status":"Healthy"}`.

| Service               | Health URL                          |
|-----------------------|-------------------------------------|
| ServerService         | http://localhost:5000/health        |
| ClientService user 1  | http://localhost:5001/health        |
| ClientService user 2  | http://localhost:5002/health        |
| ClientService user 3  | http://localhost:5003/health        |
| ClientService user 4  | http://localhost:5004/health        |
| ClientService user 5  | http://localhost:5005/health        |

You can also check container health status at a glance:

```bash
docker-compose ps
# All application containers should show (healthy) next to their running state.
```

A ClientService instance will report `{"status":"Unhealthy"}` (HTTP 503) if its
`ClientIdentity__UserId` environment variable is missing or not a valid GUID.

## Running Tests

```bash
dotnet test MicroservicesSync.Tests/MicroservicesSync.Tests.csproj
```
