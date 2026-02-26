---
project_name: Microserices-Sync
user_name: Eugen
communication_language: English
document_output_language: English
user_skill_level: intermediate
output_folder: c:/Eugen Files/Projects/BMAD-Microserices-Sync/_bmad-output
planning_artifacts: c:/Eugen Files/Projects/BMAD-Microserices-Sync/_bmad-output/planning-artifacts
implementation_artifacts: c:/Eugen Files/Projects/BMAD-Microserices-Sync/_bmad-output/implementation-artifacts
project_knowledge: c:/Eugen Files/Projects/BMAD-Microserices-Sync/docs
---

# Project Context

## Technology Stack
- .NET 10 ASP MVC for microservices
- SQL Server (ServerService)
- SQLite (ClientService)
- Docker & docker-compose for deployment

## Architecture
- Two microservices: ServerService (backend API), ClientService (emulates HoloLens app)
- ServerService: full CRUD for all tables, including Measurements (admin can update, resolve conflicts, or delete records; simulated admin, no authentication/authorization).
- ClientService: full CRUD for Measurements, read-only for all other tables.
- All tables are synchronized between services. Data is created on one service and synced to the other, even if read-only there.
- Measurements from all clients are synchronized to ServerService, and then all Measurements are synchronized back to all clients, so each client knows measurements made by other instances.
- Multiple ClientService instances for sync/concurrency testing.
- Docker volumes for database and logs.

## Synchronization
- Offline data collection, later sync
- Sync protocol: RESTful APIs, change tracking, conflict resolution
- Entity Framework Core recommended
- No authentication or authorization; all user/admin actions are simulated for research purposes only.

## Implementation Rules
- Maintain consistent models and table structures across services
- Use versioning/timestamps for change tracking
- Design API endpoints for CRUD and sync
- Ensure data integrity and handle conflicts
- Use Docker volumes for persistence
- Measurements table should use GUIDs for primary keys to ensure uniqueness across clients
- Foreign keys (CellId, UserId) reference centrally managed tables, ensuring referential integrity

## Unique Requirements
- Emulate offline/online sync scenario
- No device hardware required
- No real users or admin; no authentication/authorization
- Focus on methodology and working solution

---
This file is the authoritative project context for all AI agents. Update as needed to reflect new rules or decisions.