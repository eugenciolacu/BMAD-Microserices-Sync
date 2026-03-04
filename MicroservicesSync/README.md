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

## Scenario Parameters

Control experiment inputs via environment variables in `docker-compose.yml` — no recompilation needed.

| Environment Variable               | Config path (ASP.NET notation)              | Default | Description                                                       |
|------------------------------------|---------------------------------------------|---------|-------------------------------------------------------------------|
| `SyncOptions__MeasurementsPerClient` | `SyncOptions:MeasurementsPerClient`        | `10`    | Number of measurements generated per ClientService per scenario run |
| `SyncOptions__BatchSize`           | `SyncOptions:BatchSize`                     | `5`     | Records per in-memory batch during a push/pull sync operation     |

To override for a single run, edit the matching `environment:` entry under the relevant service in `docker-compose.yml`, then run `docker-compose up`.

### Changing Client Count

The default topology uses 5 ClientService instances (`clientservice_user1` through `clientservice_user5`), as defined in ADR-002.

To add a 6th client:
1. Copy any `clientservice_userN` block in `docker-compose.yml`, rename it `clientservice_user6`, and update the port mapping (`"5006:8080"`), SQLite path, `ClientIdentity__UserId` (new stable GUID), and volume names.
2. Add the matching volume declarations at the bottom of `docker-compose.yml`.
3. Ensure the GUID matches a seeded user entry (Story 1.4 seed data).

To remove a client: delete its service block and its volumes declaration.

## Running Tests

```bash
dotnet test MicroservicesSync.Tests/MicroservicesSync.Tests.csproj
```
