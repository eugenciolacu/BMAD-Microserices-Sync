---
project_name: Microserices-Sync
user_name: Eugen
communication_language: English
date: 2026-02-26
---



# Brainstorming Session: Project Elaboration

## Project Overview
Microserices-Sync emulates offline/online data synchronization between a client (HoloLens-like, ClientService) and a backend server (ServerService) using two microservices. The solution is containerized with Docker and uses .NET 10 ASP MVC, SQL Server, and SQLite.

## Database Schema & Data Flow

### Tables & Synchronization
- **Buildings, Rooms, Surfaces, Cells, Users**: Data is originally created and managed (full CRUD) on ServerService, then synchronized to ClientService, where it is read-only. Any updates or new records are made on ServerService and propagated to all clients through synchronization process.
- **Measurements**: Data is created and managed (full CRUD) on ClientService, then synchronized to ServerService, where it is also full CRUD (admin can update, resolve conflicts, or delete records as needed). Measurements from ServerService are also synchronized back to ClientService, so each client knows measurements made by other instances.

### Data Flow
- Users insert and manage all data on ServerService, including Measurements (admin can update or resolve conflicts). This data is periodically synchronized to all ClientService instances.
- ClientService pulls all reference data (Buildings, Rooms, Surfaces, Cells, Users, Measurements) from ServerService to stay up-to-date.
- ClientService generates random Measurement data for a specific user (configured per instance) and allows full CRUD locally.
- When triggered, ClientService pushes new or updated Measurements to ServerService.
- ServerService synchronizes all Measurements (including those from other clients) back to ClientService, so each client can see measurements made by other instances.
- ServerService allows admin CRUD operations on Measurements.

### Sync & Conflict Considerations
- All tables are synchronized between services, but CRUD permissions differ: ServerService is the source of truth for reference data, ClientService is the source for Measurements.
- Conflicts for Measurements occur when different ClientService instances (with predefined users) provide measurement data for the same cell. This results in a cell having two or more measurements from different users.
- Admin resolution options:
	- The admin can manually select which measurement to keep for each cell.
	- Alternatively, all measurements can be kept, but for reporting (not covered in this project), only the last one or a specific rule could be used.
	- Another scenario is to automatically keep only the last measurement for each cell.
- Reference data conflicts are minimized since only ServerService can create or update those records.

## Key Implementation Notes
- ServerService: CRUD for all tables, including Measurements (admin can update, resolve conflicts, or delete records).
- ClientService: CRUD for Measurements (synced to server), read-only for all other tables (synced from server).
- All tables are synchronized, but only one service has user-facing write permissions for each table (admin can update Measurements on ServerService).
- Measurements table should use GUIDs for primary keys to ensure uniqueness across clients.
- Foreign keys (CellId, UserId) reference centrally managed tables, ensuring referential integrity.

---

## Idea Generation

### 1. Synchronization Strategies
- Implement RESTful sync endpoints with change tracking (timestamps, version numbers)
- Use a sync queue or staging table for pending changes
- Support client-initiated sync (push and pull): The client initiates synchronization when online, sending its changes (push) and requesting updates (pull) from the server. This is standard for offline-first/mobile scenarios.
- Server-initiated sync is not required: The server cannot reliably reach offline or NAT-protected clients, and there is no practical benefit in this architecture. All sync should be initiated by the client.
- Conflict resolution: last-write-wins, manual merge, or custom rules
- Optionally, use a sync token or watermark to track last sync state

### 2. Data Integrity & Consistency
- Use GUIDs for primary keys to avoid collisions
- Implement soft deletes and audit trails for traceability
- Validate data before accepting sync payloads
- Log all sync operations for debugging and rollback

### 3. Scalability & Testing
- Simulate multiple ClientService instances to test concurrency
- Use Docker Compose to orchestrate multi-container scenarios
- Automate random data generation for CRUD tables on ClientService

### 4. User Experience & Operations
- Use jqGrid for interactive data views in ASP.NET MVC
- Provide a simple UI for triggering sync and viewing sync status
- Expose logs and sync history for transparency
- Manual conflict resolution is simulated for research purposes; there are no real users or admin, and no authentication or authorization is intended.
- Support scheduled or on-demand sync modes

### 5. Extensibility & Future-Proofing
- Design models and APIs for easy extension (new tables, fields)
- Abstract sync logic for reuse in real device scenarios
- Document all sync rules and edge cases in project-context.md

---
This document captures the initial brainstorming ideas for the project. Continue to expand and refine as the project evolves.
