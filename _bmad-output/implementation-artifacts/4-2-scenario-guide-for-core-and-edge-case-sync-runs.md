# Story 4.2: Scenario Guide for Core and Edge-Case Sync Runs

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a scenario guide embedded in the README for the core and edge-case sync runs,
so that I know exactly which steps to execute and how to verify outcomes.

## Acceptance Criteria

1. **Given** I scroll to the scenarios section of the README  
   **When** I read the instructions for the core multi-client scenario (5 clients × 10 measurements)  
   **Then** I see a numbered sequence that covers clean baseline reset, measurement generation, transactional batched push, transactional batched pull, and verification steps.

2. **Given** I follow the core scenario instructions from start to finish  
   **When** I compare measurement datasets on ServerService and all ClientService instances using the documented grids and tools  
   **Then** I can confirm that all services converge to the same dataset if the system is working correctly.

3. **Given** the README also documents at least one edge-case scenario variant  
   **When** I follow the edge-case instructions (including any special configuration or timing)  
   **Then** I can reproduce the variant run and verify convergence or expected behavior using the same verification guidance.

## Tasks / Subtasks

- [x] **Task 1: Audit README to confirm insertion point** (AC: #1, #2, #3)
  - [x] 1.1 Open `MicroservicesSync/README.md`. Read the full file from top to bottom.
  - [x] 1.2 Confirm the section order: Prerequisites → Quick Start → Verifying the Environment is Healthy → Scenario Parameters → Reset to Clean Baseline → Running Tests → Direct Database Inspection → Viewing Sync Logs → Troubleshooting Unexpected Sync Outcomes.
  - [x] 1.3 Confirm there is **no existing** "Running Sync Scenarios" or similar scenario guide section. If any partial scenario content exists, note it rather than duplicating.

- [x] **Task 2: Insert "Running Sync Scenarios" section** (AC: #1, #2, #3)
  - [x] 2.1 Insert a new `## Running Sync Scenarios` section **immediately after** the `## Reset to Clean Baseline` section and **before** the `## Running Tests` section. See Dev Notes for the exact content.
  - [x] 2.2 Write the **Scenario A** subsection: standard 5-client × 10-measurement sync run. See Dev Notes for exact steps, expected outcomes, and API alternatives.
  - [x] 2.3 Write the **Scenario B** subsection: edge-case 3-client × 5-measurement reduced-volume variant (BatchSize=2). See Dev Notes for exact steps, configuration instructions, and revert guidance.
  - [x] 2.4 Cross-reference (do NOT duplicate) existing README sections for deep verification: link to `#reset-to-clean-baseline`, `#direct-database-inspection`, `#viewing-sync-logs`, and `#troubleshooting-unexpected-sync-outcomes`.

- [x] **Task 3: Final consistency review** (AC: #1, #2, #3)
  - [x] 3.1 Read the updated README from top to bottom as a first-time developer would. Verify the flow: Prerequisites → Quick Start → Health check → Parameters → **Reset** → **Scenarios** → Tests → Deep diagnostics reads logically.
  - [x] 3.2 Verify all anchor links in the new section are correct (e.g., `#reset-to-clean-baseline`, `#direct-database-inspection`). GitHub Markdown anchors are lowercase with spaces replaced by hyphens.
  - [x] 3.3 Check that no curl commands or step sequences are duplicated from the "Reset to Clean Baseline" section.
  - [x] 3.4 Confirm this is a **documentation-only story**: no `.cs` files, no `Dockerfiles`, no `docker-compose.yml` (the edge-case scenario explains how to temporarily edit it — the story file itself does NOT make those edits), no test project files modified.

## Dev Notes

### Context: README State Entering Story 4.2

Story 4.1 (done) made three targeted additions to `MicroservicesSync/README.md`. The README now has the following structure, in order:

| Section | Added in | Notes |
|---|---|---|
| Prerequisites | Story 4.1 | Full tool table — Docker, Git, .NET 10 SDK, VS 2022, SSMS, DB Browser |
| Quick Start — Before your first run | Story 4.1 | Clone + `.env` creation preamble |
| Quick Start — Start the services | Story 1.1 | `docker-compose up --build`, `up`, `down` |
| Verifying the Environment is Healthy | Story 1.2 | `/health` endpoints |
| Accessing the Service Home Pages | Story 4.1 | Added to Verifying section; home page URLs 5000–5005 with expected content |
| Scenario Parameters | Story 1.3 | `MeasurementsPerClient`, `BatchSize`, client count instructions |
| Reset to Clean Baseline | Story 1.5 | curl reset + pull-reference-data + UI buttons + expected entity counts table |
| Running Tests | Epic 1 | `dotnet test` command |
| Direct Database Inspection | Story 3.3 | SSMS + DB Browser; `docker cp` for SQLite; diagnostic SQL queries |
| Viewing Sync Logs | Story 3.4 | `docker logs`, correlation ID tracing, log field descriptions |
| Troubleshooting Unexpected Sync Outcomes | Story 3.5 | 5-step investigation flow with symptom checklist |

**This story adds exactly one new section:** `## Running Sync Scenarios`, inserted between `## Reset to Clean Baseline` and `## Running Tests`. No other sections are modified, reorganized, or deleted.

### Insertion Point

Insert the new section immediately after the last content line of `## Reset to Clean Baseline` (which ends with the "expected clean state" entity-count table and its note about the Direct Database Inspection baseline) and before the `## Running Tests` heading.

### Scenario A: Standard 5-Client × 10-Measurement Sync Run

**Default parameters (no docker-compose.yml changes needed):**

- Clients: 5 (`clientservice_user1` through `clientservice_user5`)
- Measurements per client: 10 (`SyncOptions__MeasurementsPerClient=10`)
- Batch size: 5 (`SyncOptions__BatchSize=5`)
- Expected total after convergence: **50 measurements** on all 6 services

**API endpoints implicated:**

| Operation | ClientService endpoint | UI button |
|---|---|---|
| Generate measurements | `POST /api/v1/measurements/generate` | **Generate Measurements** |
| Push to ServerService | `POST /api/v1/measurements/push` | **Push Measurements** |
| Pull from ServerService | `POST /api/v1/measurements/pull` | **Pull Measurements** |
| Reset client DB | `POST /api/v1/admin/reset` | **Reset Client DB** |
| Pull reference data | `POST /api/v1/admin/pull-reference-data` | **Pull Reference Data** |

**Exact README content for Scenario A:**

```markdown
## Running Sync Scenarios

> **Prerequisites for all scenarios:** All containers running (`docker-compose up`). Environment healthy (see [Verifying the Environment is Healthy](#verifying-the-environment-is-healthy)).

### Scenario A: Standard 5-Client × 10-Measurement Sync Run

This is the default scenario. No configuration changes are required — the docker-compose topology and environment defaults are pre-set for 5 clients × 10 measurements × batch size 5.

**Expected outcome:** After a complete push + pull cycle, ServerService and all 5 ClientService instances each hold **50 measurements** (5 clients × 10 measurements each).

**Step 1 — Reset to clean baseline**

Follow the [Reset to Clean Baseline](#reset-to-clean-baseline) steps to ensure all services start from a consistent state before running this scenario.

**Step 2 — Generate measurements (all 5 clients)**

On each ClientService home page (`http://localhost:5001` through `http://localhost:5005`), click **Generate Measurements**.

Expected result: a success message confirms 10 new measurements were created on that client, tagged with its own user identity.

> API alternative: `curl -X POST http://localhost:5001/api/v1/measurements/generate` (repeat for 5002–5005)

**Step 3 — Push measurements to ServerService (all 5 clients)**

On each ClientService home page, click **Push Measurements** (any order).

Expected result:
- Each push returns a success message.
- After all 5 clients push, the **Measurements** grid on `http://localhost:5000` shows **50 records**.

> API alternative: `curl -X POST http://localhost:5001/api/v1/measurements/push` (repeat for 5002–5005)

**Step 4 — Pull consolidated measurements (all 5 clients)**

On each ClientService home page, click **Pull Measurements** (any order).

Expected result: each ClientService **Measurements** grid shows **50 records** — including all measurements generated by the other four clients.

> API alternative: `curl -X POST http://localhost:5001/api/v1/measurements/pull` (repeat for 5002–5005)

**Step 5 — Verify convergence**

On each ClientService home page, click **Verify Convergence**.

Expected result: `✓ Converged: this client has 50 measurements, server has 50.`

For deeper verification (SQL-level row counts and duplicate checks), see [Direct Database Inspection](#direct-database-inspection). For diagnosing unexpected outcomes, see [Troubleshooting Unexpected Sync Outcomes](#troubleshooting-unexpected-sync-outcomes).
```

### Scenario B: Edge-Case 3-Client × 5-Measurement Reduced-Volume Variant

From Story 2.4 Dev Notes, the documented edge-case is: **3 clients × 5 measurements × BatchSize=2**. This exercises:
- **Uneven batching**: 5 measurements ÷ batch size 2 = 2 full batches + 1 remainder batch, confirming the single transaction handles the remainder correctly.
- **Reduced client count**: 3 active clients (users 1–3) with clients 4–5 sitting out at baseline, confirming client isolation.

The edge-case requires temporarily editing `docker-compose.yml` for three services. Per the established pattern in the `## Scenario Parameters` section: "To override for a single run, edit the matching `environment:` entry under the relevant service in `docker-compose.yml`, then run `docker-compose up`."

**Only 3 services need changes** (`clientservice_user1`, `clientservice_user2`, `clientservice_user3`):
- `SyncOptions__MeasurementsPerClient=5`
- `SyncOptions__BatchSize=2`

Leave `clientservice_user4` and `clientservice_user5` with their default values. After the variant run, revert all 3 services back to their original values.

**Expected outcome:** After push + pull cycle for clients 1–3, each of those clients and ServerService holds **15 measurements** (3 × 5). Clients 4 and 5 remain at 0 measurements.

**Exact README content for Scenario B (append after Scenario A):**

```markdown
### Scenario B: Edge-Case 3-Client × 5-Measurement Reduced-Volume Variant

This variant exercises uneven batch sizes (remainder batch handling) and reduced client count to validate that transactional sync handles partial batches correctly.

**Configuration change required before starting.** Stop the environment first:

```bash
docker-compose down
```

Edit `docker-compose.yml`. In the `environment:` block for `clientservice_user1`, `clientservice_user2`, and `clientservice_user3`, locate the `SyncOptions__MeasurementsPerClient` and `SyncOptions__BatchSize` entries and set:

```yaml
- SyncOptions__MeasurementsPerClient=5
- SyncOptions__BatchSize=2
```

Leave `clientservice_user4` and `clientservice_user5` with their original defaults.

**Step 1 — Start the environment with updated configuration**

```bash
docker-compose up
```

**Step 2 — Reset clients 1–3 to clean baseline**

```bash
curl -X POST http://localhost:5001/api/v1/admin/reset
curl -X POST http://localhost:5002/api/v1/admin/reset
curl -X POST http://localhost:5003/api/v1/admin/reset
```

Then reload reference data on each:

```bash
curl -X POST http://localhost:5001/api/v1/admin/pull-reference-data
curl -X POST http://localhost:5002/api/v1/admin/pull-reference-data
curl -X POST http://localhost:5003/api/v1/admin/pull-reference-data
```

**Step 3 — Generate, push, and pull for clients 1–3 only**

On `http://localhost:5001`, `http://localhost:5002`, and `http://localhost:5003`:

1. Click **Generate Measurements** → expect 5 measurements per client.
2. Click **Push Measurements** → expect success.
3. After all 3 have pushed, the **Measurements** grid on ServerService (`http://localhost:5000`) should show **15 records** (3 × 5).
4. Click **Pull Measurements** on each of the three clients.
5. Click **Verify Convergence** → each should report `✓ Converged: this client has 15 measurements, server has 15.`

> Clients 4 and 5 (`http://localhost:5004`, `http://localhost:5005`) remain at baseline with 0 measurements, confirming client isolation.

**Step 4 — Restore default configuration**

Stop the environment, revert `docker-compose.yml` for clients 1–3 to the original values (`SyncOptions__MeasurementsPerClient=10` and `SyncOptions__BatchSize=5`), and restart as normal.
```

### Scope Guard: What NOT to Do

- **DO NOT** duplicate the reset CLI commands — they already exist in full in `## Reset to Clean Baseline`. Reference that section with a link; do not copy the curl commands.
- **DO NOT** duplicate the database inspection queries — they already exist in `## Direct Database Inspection`. The Scenario A/B steps reference the section with a link only.
- **DO NOT** duplicate log commands — they already exist in `## Viewing Sync Logs`.
- **DO NOT** modify `docker-compose.yml` as part of this story. The Dev Notes explain what temporary edits the developer makes when running Scenario B; the story itself does NOT make those edits.
- **DO NOT** create a separate scenario file. FR14 requires the guide to be embedded in the existing `README.md`.
- **DO NOT** modify any `.cs` files, `Dockerfiles`, `docker-compose.override.yml`, test project files, or any file outside `MicroservicesSync/README.md`.

### Section Anchor Reference

When writing cross-reference links in the new section, use these anchors (GitHub Markdown heading anchors):

| Target section | Anchor |
|---|---|
| Reset to Clean Baseline | `#reset-to-clean-baseline` |
| Verifying the Environment is Healthy | `#verifying-the-environment-is-healthy` |
| Direct Database Inspection | `#direct-database-inspection` |
| Viewing Sync Logs | `#viewing-sync-logs` |
| Troubleshooting Unexpected Sync Outcomes | `#troubleshooting-unexpected-sync-outcomes` |
| Scenario Parameters | `#scenario-parameters` |

### Previous Story Intelligence (Story 4.1)

- Story 4.1 established the pattern for Epic 4: **targeted additions to specific locations** in an existing README, no rewrites.
- Story 4.1 modified three sections: Prerequisites, Quick Start (preamble), and Verifying the Environment is Healthy (home pages subsection). Nothing else was touched.
- Story 4.1's scope guard: "Do NOT reorganize or restructure the body of the README beyond the three targeted changes." Story 4.2 follows the same discipline — one new section, carefully placed, no restructuring.
- The README currently ends with `## Troubleshooting Unexpected Sync Outcomes` (very long section with 5 investigation steps). Story 4.2 inserts its content in the middle of the file, not at the end.

### Project Structure Notes

**File to modify:**
- `MicroservicesSync/README.md` — insert `## Running Sync Scenarios` section after `## Reset to Clean Baseline` and before `## Running Tests`

**Files NOT to modify:**
- Any `*.cs` files anywhere in the solution
- `docker-compose.yml` or `docker-compose.override.yml`
- Any Dockerfile
- Any test project files
- Any planning or implementation artifact files

### References

- [Source: MicroservicesSync/README.md] — Full current README; confirms no scenario guide section exists
- [Source: _bmad-output/planning-artifacts/epics.md#Story 4.2] — Acceptance criteria origin and FR14 mapping
- [Source: _bmad-output/implementation-artifacts/2-4-edge-case-multi-client-scenario-variant-with-batching.md#Dev Notes/Edge-Case Scenario Steps] — Exact edge-case variant: 3 clients × 5 measurements × BatchSize=2; step-by-step instructions and rationale
- [Source: _bmad-output/implementation-artifacts/2-1-client-side-measurement-generation-per-instance.md] — Generate API: `POST /api/v1/measurements/generate`
- [Source: _bmad-output/implementation-artifacts/2-2-transactional-batched-server-side-measurement-push.md] — Push API: `POST /api/v1/measurements/push`; single HTTP call with server-side batching in one transaction
- [Source: _bmad-output/implementation-artifacts/2-3-transactional-batched-client-side-pull-of-consolidated-measurements.md] — Pull API: `POST /api/v1/measurements/pull`; client-side single transaction across all batches
- [Source: _bmad-output/implementation-artifacts/4-1-single-entry-readme-with-prerequisites-and-quickstart.md] — Story 4.1 dev notes for README state context and section order
- [Source: _bmad-output/implementation-artifacts/epic-3-retro-2026-03-12.md#Challenge 4 and Next Epic Preview] — README context; confirms "Running Sync Scenarios" must be a new section
- [Source: _bmad-output/project-context.md#Technology Stack & Versions] — 5-client topology: `clientservice_user1`–`user5` on ports 5001–5005
- [Source: MicroservicesSync/ClientService/Controllers/MeasurementsController.cs] — Confirmed routes: `POST api/v1/measurements/generate`, `POST api/v1/measurements/push`, `POST api/v1/measurements/pull`

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

### Completion Notes List

- Inserted `## Running Sync Scenarios` section into `MicroservicesSync/README.md` immediately after `## Reset to Clean Baseline` and before `## Running Tests`.
- Scenario A (5 clients × 10 measurements × batch 5): 5-step numbered sequence covering reset, generate, push, pull, and convergence verification with UI button paths and API alternatives.
- Scenario B (3 clients × 5 measurements × batch 2): edge-case variant with docker-compose.yml configuration instructions, targeted reset steps for clients 1–3, full push/pull/verify sequence, and revert guidance.
- All cross-references use anchor links (`#reset-to-clean-baseline`, `#verifying-the-environment-is-healthy`, `#direct-database-inspection`, `#troubleshooting-unexpected-sync-outcomes`); no content was duplicated from existing sections.
- **Verify Convergence** button output `✓ Converged: this client has N measurements, server has N.` confirmed against actual ClientService UI code.
- Documentation-only: no `.cs`, Docker, `docker-compose.yml`, or test project files modified.
- **Code review fixes (2026-03-12):** corrected Scenario B Step 2 to use a Reset to Clean Baseline link instead of duplicated curl commands (scope guard fix + ServerService reset gap); added `#viewing-sync-logs` cross-reference to Scenario A Step 5 footer (task 2.4 completion); split prerequisites note into separate Scenario A and Scenario B entries to eliminate contradiction with Scenario B's `docker-compose down` first action.

### File List

- `MicroservicesSync/README.md` — inserted `## Running Sync Scenarios` section (Scenario A and Scenario B)

### Change Log

- 2026-03-12: Inserted `## Running Sync Scenarios` section into README with Scenario A (standard 5-client × 10-measurement run) and Scenario B (edge-case 3-client × 5-measurement × BatchSize=2 variant). Documentation-only change.
- 2026-03-12: Code review fixes — Scenario B Step 2 rewritten to reference Reset to Clean Baseline (removes duplicated curl commands and adds missing ServerService reset); added `#viewing-sync-logs` link to Scenario A Step 5 footer; split prerequisites note into per-scenario entries.
