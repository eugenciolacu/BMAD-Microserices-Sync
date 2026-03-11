# Story 3.2: Measurement Inspection via jqGrid on Both Services

Status: done

## Story

As a developer,
I want jqGrid-based views of measurements on both ServerService and ClientService,
So that I can manually inspect and compare measurement records after sync.

## Acceptance Criteria

1. **Given** measurements exist on ServerService and on one or more ClientService instances  
   **When** I open the Measurements grid on ServerService  
   **Then** I can page, sort, and filter measurements, and each row shows key fields (IDs, client/user, timestamps, and values) needed for comparison.

2. **Given** I open the Measurements grid on a ClientService instance  
   **When** I apply the same sort and filter conditions as on ServerService  
   **Then** the set of visible measurement rows matches what I see on ServerService after a successful sync.

3. **Given** I need to investigate a specific measurement  
   **When** I search or filter by its unique identifier or client/user  
   **Then** I can locate the same record on both ServerService and ClientService to confirm it is present and aligned.

## Tasks / Subtasks

- [x] **Task 1: Add jqGrid client-side libraries via LibMan to both services** (AC: #1, #2)
  - [x] 1.1 Create `ServerService/libman.json` with the following content:
    ```json
    {
      "version": "1.0",
      "defaultProvider": "cdnjs",
      "libraries": [
        {
          "library": "jqueryui@1.14.1",
          "destination": "wwwroot/lib/jqueryui/"
        },
        {
          "library": "free-jqgrid@4.15.5",
          "destination": "wwwroot/lib/free-jqgrid/"
        }
      ]
    }
    ```
    **Note**: jQuery 3.7.1 is already present at `wwwroot/lib/jquery/` (shipped with the ASP.NET Core template). Only jQuery UI and free-jqGrid need to be added. Do NOT add a duplicate jQuery entry.
  - [x] 1.2 Create `ClientService/libman.json` with identical content to 1.1.
  - [x] 1.3 Restore libraries for ServerService:
    ```powershell
    cd "MicroservicesSync/ServerService"
    dotnet tool restore   # if libman CLI not yet available
    libman restore
    ```
    Verify the following directories are created and populated:
    - `ServerService/wwwroot/lib/jqueryui/` (contains `jquery-ui.min.js`, `themes/base/jquery-ui.min.css`, etc.)
    - `ServerService/wwwroot/lib/free-jqgrid/` (contains `js/jquery.jqgrid.min.js`, `css/ui.jqgrid.min.css`, etc.)
  - [x] 1.4 Restore libraries for ClientService:
    ```powershell
    cd "MicroservicesSync/ClientService"
    libman restore
    ```
    Verify the same directories are created under `ClientService/wwwroot/lib/`.
  - [x] 1.5 **SCOPE GUARD**: The `_Layout.cshtml` files already include jQuery (`~/lib/jquery/dist/jquery.min.js`). Do NOT add jQuery via LibMan — it would create a duplicate. The jqGrid and jQuery UI CSS/JS will be referenced from the Measurements view page, not from `_Layout.cshtml`.
  - [x] 1.6 Run `dotnet build MicrosericesSync.sln` — 0 errors.

- [x] **Task 2: Add `JqGridFilter` model and `JqGridHelper` to shared infrastructure** (AC: #1, #2, #3)
  - [x] 2.1 Create `Sync.Infrastructure/Grid/JqGridFilter.cs`:
    ```csharp
    namespace Sync.Infrastructure.Grid;

    /// <summary>
    /// Represents the filter structure sent by jqGrid's filter toolbar.
    /// Deserialized from the 'filters' query string parameter.
    /// </summary>
    public class JqGridFilter
    {
        public string GroupOp { get; set; } = "AND";
        public List<JqGridFilterRule> Rules { get; set; } = new();
    }

    public class JqGridFilterRule
    {
        public string Field { get; set; } = string.Empty;
        public string Op { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }
    ```
  - [x] 2.2 Create `Sync.Infrastructure/Grid/JqGridHelper.cs` with a static `ApplyFilters<T>` method that builds LINQ expression trees from `JqGridFilter`. This is adapted from the reference `Repository.cs` but extended to handle `Guid`, `Guid?`, `decimal`, `DateTime`, and `DateTime?` types in addition to `string` and `int`:
    ```csharp
    using System.Linq.Expressions;

    namespace Sync.Infrastructure.Grid;

    /// <summary>
    /// Builds dynamic LINQ Where expressions from jqGrid filter structures.
    /// Supports: string (cn, eq, ne, bw, ew), int/int? (eq, ne, lt, le, gt, ge),
    /// decimal/decimal? (eq, ne, lt, le, gt, ge), Guid/Guid? (eq, ne),
    /// DateTime/DateTime? (eq, ne, lt, le, gt, ge).
    /// </summary>
    public static class JqGridHelper
    {
        /// <summary>
        /// Applies jqGrid filter rules to an IQueryable, building a combined Where clause.
        /// </summary>
        public static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, JqGridFilter filter)
        {
            if (filter.Rules.Count == 0)
                return query;

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? combined = null;

            foreach (var rule in filter.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Field) || string.IsNullOrWhiteSpace(rule.Data))
                    continue;

                // Normalize field name: capitalize first letter to match C# property names
                var fieldName = char.ToUpperInvariant(rule.Field[0]) + rule.Field[1..];

                // Validate property exists on type T (prevent arbitrary property access)
                var propInfo = typeof(T).GetProperty(fieldName);
                if (propInfo == null)
                    continue;

                var property = Expression.Property(parameter, fieldName);
                var propertyType = propInfo.PropertyType;
                var expr = BuildFilterExpression(property, propertyType, rule);
                if (expr == null)
                    continue;

                combined = combined == null
                    ? expr
                    : filter.GroupOp.Equals("OR", StringComparison.OrdinalIgnoreCase)
                        ? Expression.OrElse(combined, expr)
                        : Expression.AndAlso(combined, expr);
            }

            if (combined != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
                query = query.Where(lambda);
            }

            return query;
        }

        /// <summary>
        /// Applies dynamic sorting to an IQueryable using expression trees.
        /// </summary>
        public static IQueryable<T> ApplySort<T>(IQueryable<T> query, string? sortBy, string? sortOrder)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
                return query;

            var fieldName = char.ToUpperInvariant(sortBy[0]) + sortBy[1..];
            var propInfo = typeof(T).GetProperty(fieldName);
            if (propInfo == null)
                return query;

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, fieldName);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase)
                ? "OrderByDescending"
                : "OrderBy";

            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                [typeof(T), property.Type],
                query.Expression,
                Expression.Quote(lambda));

            return query.Provider.CreateQuery<T>(resultExpression);
        }

        private static Expression? BuildFilterExpression(
            MemberExpression property, Type propertyType, JqGridFilterRule rule)
        {
            var op = rule.Op.ToLowerInvariant();

            // --- string ---
            if (propertyType == typeof(string))
            {
                var constant = Expression.Constant(rule.Data);
                return op switch
                {
                    "eq" => Expression.Equal(property, constant),
                    "ne" => Expression.NotEqual(property, constant),
                    "cn" => Expression.Call(property,
                        typeof(string).GetMethod("Contains", [typeof(string)])!, constant),
                    "bw" => Expression.Call(property,
                        typeof(string).GetMethod("StartsWith", [typeof(string)])!, constant),
                    "ew" => Expression.Call(property,
                        typeof(string).GetMethod("EndsWith", [typeof(string)])!, constant),
                    _ => null
                };
            }

            // --- Guid / Guid? ---
            if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
            {
                if (!Guid.TryParse(rule.Data, out var guidValue))
                    return null;
                var isNullable = propertyType == typeof(Guid?);
                Expression prop = isNullable ? Expression.Property(property, "Value") : property;
                var constant = Expression.Constant(guidValue, typeof(Guid));
                return op switch
                {
                    "eq" => Expression.Equal(prop, constant),
                    "ne" => Expression.NotEqual(prop, constant),
                    _ => null
                };
            }

            // --- decimal / decimal? ---
            if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
            {
                if (!decimal.TryParse(rule.Data, System.Globalization.CultureInfo.InvariantCulture, out var decValue))
                    return null;
                var isNullable = propertyType == typeof(decimal?);
                Expression prop = isNullable ? Expression.Property(property, "Value") : property;
                var constant = Expression.Constant(decValue, typeof(decimal));
                return BuildComparisonExpression(prop, constant, op);
            }

            // --- DateTime / DateTime? ---
            if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                if (!DateTime.TryParse(rule.Data, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dtValue))
                    return null;
                var isNullable = propertyType == typeof(DateTime?);
                Expression prop = isNullable ? Expression.Property(property, "Value") : property;
                var constant = Expression.Constant(dtValue, typeof(DateTime));
                return BuildComparisonExpression(prop, constant, op);
            }

            // --- int / int? ---
            if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                if (!int.TryParse(rule.Data, out var intValue))
                    return null;
                var isNullable = propertyType == typeof(int?);
                Expression prop = isNullable ? Expression.Property(property, "Value") : property;
                var constant = Expression.Constant(intValue, typeof(int));
                return BuildComparisonExpression(prop, constant, op);
            }

            return null;
        }

        private static Expression? BuildComparisonExpression(
            Expression property, Expression constant, string op)
        {
            return op switch
            {
                "eq" => Expression.Equal(property, constant),
                "ne" => Expression.NotEqual(property, constant),
                "lt" => Expression.LessThan(property, constant),
                "le" => Expression.LessThanOrEqual(property, constant),
                "gt" => Expression.GreaterThan(property, constant),
                "ge" => Expression.GreaterThanOrEqual(property, constant),
                _ => null
            };
        }
    }
    ```
  - [x] 2.3 Place these files in `Sync.Infrastructure/Grid/` — a new folder. This is shared code used by both ServerService and ClientService controllers. It does NOT require any DbContext dependency or DI registration.
  - [x] 2.4 Run `dotnet build MicrosericesSync.sln` — 0 errors.
  - [x] 2.5 **SCOPE GUARD**: Do NOT create a generic repository or generic service layer. The `JqGridHelper` is a standalone static utility. Controllers call it directly against their `DbSet<T>` queries — consistent with the direct-DbContext-injection pattern used throughout this project.

- [x] **Task 3: Add `MeasurementsGridController` to ServerService** (AC: #1, #3)
  - [x] 3.1 Create `ServerService/Controllers/MeasurementsGridController.cs`:
    ```csharp
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Sync.Domain.Entities;
    using Sync.Infrastructure.Data;
    using Sync.Infrastructure.Grid;
    using System.Text.Json;

    namespace ServerService.Controllers;

    /// <summary>
    /// jqGrid-compatible paged/filtered/sorted API for Measurement inspection on ServerService.
    /// Story 3.2 — AC#1, AC#3.
    /// </summary>
    [ApiController]
    [Route("api/v1/measurements-grid")]
    public class MeasurementsGridController : ControllerBase
    {
        private readonly ServerDbContext _db;
        private readonly ILogger<MeasurementsGridController> _logger;

        public MeasurementsGridController(ServerDbContext db, ILogger<MeasurementsGridController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // NOTE (M3.2): MeasurementsGridController injects ServerDbContext directly.
        // Consistent with the acknowledged deviation in AdminController (NOTE M4),
        // SyncMeasurementsController, and SyncRunsController (NOTE M3.1).
        // Acceptable for this experiment scope.

        /// <summary>
        /// Returns paged, sorted, filtered measurements for jqGrid consumption.
        /// Query params: page (1-based), pageSize, sortBy, sortOrder, filters (JSON).
        /// </summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? filters = null,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            IQueryable<Measurement> query = _db.Measurements.AsNoTracking();

            // Apply jqGrid filters
            if (!string.IsNullOrWhiteSpace(filters))
            {
                try
                {
                    var gridFilter = JsonSerializer.Deserialize<JqGridFilter>(filters,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (gridFilter != null)
                        query = JqGridHelper.ApplyFilters(query, gridFilter);
                }
                catch (JsonException)
                {
                    return BadRequest(new { message = "Invalid filter format." });
                }
            }

            // Total count (after filter, before paging)
            var totalCount = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Apply sorting
            query = JqGridHelper.ApplySort(query, sortBy, sortOrder);

            // Apply paging
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Value,
                    m.RecordedAt,
                    m.SyncedAt,
                    m.UserId,
                    m.CellId
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                data,
                totalCount,
                totalPages,
                page,
                pageSize
            });
        }

        /// <summary>
        /// Returns a single measurement by ID. Used by jqGrid view/edit operations.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var measurement = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.Id,
                    m.Value,
                    m.RecordedAt,
                    m.SyncedAt,
                    m.UserId,
                    m.CellId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (measurement == null)
                return NotFound();

            return Ok(measurement);
        }

        /// <summary>
        /// Creates a new measurement. Used by jqGrid Add dialog.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] MeasurementCreateRequest request,
            CancellationToken cancellationToken)
        {
            var entity = new Measurement
            {
                Id = Guid.NewGuid(),
                Value = request.Value,
                RecordedAt = request.RecordedAt,
                SyncedAt = null,
                UserId = request.UserId,
                CellId = request.CellId
            };

            _db.Measurements.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id });
        }

        /// <summary>
        /// Updates an existing measurement. Used by jqGrid Edit dialog.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] MeasurementUpdateRequest request,
            CancellationToken cancellationToken)
        {
            var entity = await _db.Measurements.FindAsync([id], cancellationToken);
            if (entity == null) return NotFound();

            entity.Value = request.Value;
            entity.RecordedAt = request.RecordedAt;
            entity.UserId = request.UserId;
            entity.CellId = request.CellId;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { entity.Id });
        }

        /// <summary>
        /// Deletes a measurement. Used by jqGrid Delete dialog.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var entity = await _db.Measurements.FindAsync([id], cancellationToken);
            if (entity == null) return NotFound();

            _db.Measurements.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }

    // Request DTOs — nested in same file for simplicity. No AutoMapper needed.

    public class MeasurementCreateRequest
    {
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }

    public class MeasurementUpdateRequest
    {
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }
    ```
  - [x] 3.2 The route is `api/v1/measurements-grid` (not `api/v1/measurements`) to avoid collision with the existing `MeasurementsController` on ClientService (which uses `api/v1/measurements` for sync operations). ServerService currently has no `MeasurementsController`.
  - [x] 3.3 The `Select` projection returns anonymous objects with camelCase property names by default (ASP.NET Core's `System.Text.Json` camelCase policy). This matches what jqGrid expects in the `colModel` `name` fields.
  - [x] 3.4 Run `dotnet build MicrosericesSync.sln` — 0 errors.
  - [x] 3.5 **SCOPE GUARD**: Do NOT create separate DTO classes in a `DTOs/` folder, AutoMapper profiles, or a generic service/repository. Keep it simple — anonymous projections in the controller, request DTOs co-located in the same file.

- [x] **Task 4: Add `MeasurementsGridController` to ClientService** (AC: #2, #3)
  - [x] 4.1 Create `ClientService/Controllers/MeasurementsGridController.cs`:
    ```csharp
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Sync.Domain.Entities;
    using Sync.Infrastructure.Data;
    using Sync.Infrastructure.Grid;
    using System.Text.Json;

    namespace ClientService.Controllers;

    /// <summary>
    /// jqGrid-compatible paged/filtered/sorted API for Measurement inspection on ClientService.
    /// Story 3.2 — AC#2, AC#3.
    /// Full CRUD for Measurements (ClientService has write access to Measurements).
    /// </summary>
    [ApiController]
    [Route("api/v1/measurements-grid")]
    public class MeasurementsGridController : ControllerBase
    {
        private readonly ClientDbContext _db;
        private readonly ILogger<MeasurementsGridController> _logger;

        public MeasurementsGridController(ClientDbContext db, ILogger<MeasurementsGridController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // NOTE (M3.2): MeasurementsGridController injects ClientDbContext directly.
        // Consistent with the acknowledged deviation in AdminController (NOTE M4)
        // and MeasurementsController. Acceptable for this experiment scope.

        /// <summary>
        /// Returns paged, sorted, filtered measurements for jqGrid consumption.
        /// </summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? filters = null,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            IQueryable<Measurement> query = _db.Measurements.AsNoTracking();

            // Apply jqGrid filters
            if (!string.IsNullOrWhiteSpace(filters))
            {
                try
                {
                    var gridFilter = JsonSerializer.Deserialize<JqGridFilter>(filters,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (gridFilter != null)
                        query = JqGridHelper.ApplyFilters(query, gridFilter);
                }
                catch (JsonException)
                {
                    return BadRequest(new { message = "Invalid filter format." });
                }
            }

            // Total count (after filter, before paging)
            var totalCount = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Apply sorting
            query = JqGridHelper.ApplySort(query, sortBy, sortOrder);

            // Apply paging
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Value,
                    m.RecordedAt,
                    m.SyncedAt,
                    m.UserId,
                    m.CellId
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                data,
                totalCount,
                totalPages,
                page,
                pageSize
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var measurement = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.Id,
                    m.Value,
                    m.RecordedAt,
                    m.SyncedAt,
                    m.UserId,
                    m.CellId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (measurement == null)
                return NotFound();

            return Ok(measurement);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] MeasurementCreateRequest request,
            CancellationToken cancellationToken)
        {
            var entity = new Measurement
            {
                Id = Guid.NewGuid(),
                Value = request.Value,
                RecordedAt = request.RecordedAt,
                SyncedAt = null,
                UserId = request.UserId,
                CellId = request.CellId
            };

            _db.Measurements.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] MeasurementUpdateRequest request,
            CancellationToken cancellationToken)
        {
            var entity = await _db.Measurements.FindAsync([id], cancellationToken);
            if (entity == null) return NotFound();

            entity.Value = request.Value;
            entity.RecordedAt = request.RecordedAt;
            entity.UserId = request.UserId;
            entity.CellId = request.CellId;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { entity.Id });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var entity = await _db.Measurements.FindAsync([id], cancellationToken);
            if (entity == null) return NotFound();

            _db.Measurements.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }

    // Request DTOs — nested in same file. Identical shape to ServerService's version.
    // Kept separate (not shared via Sync.Domain/Sync.Infrastructure) because these are
    // web-layer concerns and the two services ship independently.

    public class MeasurementCreateRequest
    {
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }

    public class MeasurementUpdateRequest
    {
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }
    ```
  - [x] 4.2 The ClientService version is functionally identical to ServerService's — same route (`api/v1/measurements-grid`), same endpoints, same `Select` projection. The only difference is `ClientDbContext` vs `ServerDbContext`.
  - [x] 4.3 ClientService already has an existing `MeasurementsController` at `api/v1/measurements` for sync operations (generate, push, pull, count). The new `MeasurementsGridController` at `api/v1/measurements-grid` is separate — no collision.
  - [x] 4.4 Run `dotnet build MicrosericesSync.sln` — 0 errors.
  - [x] 4.5 **SCOPE GUARD**: Do NOT share the request DTO types between the two services via a shared library. Web-layer DTOs stay in the web project. The duplication is intentional and minimal.

- [x] **Task 5: Create `gridUtils.js` shared JavaScript module for both services** (AC: #1, #2)
  - [x] 5.1 Create `ServerService/wwwroot/js/gridUtils.js` adapted from the reference `docs/code-snippets/wwwroot/js/gridUtils.js`. The module uses the IIFE pattern and provides:
    - `addKeyboardHandlers(dialogSelector, isEditOrAdd)` — Enter to submit, ESC to cancel
    - `centerDialog(dialogSelector)` — centers jqGrid dialogs in viewport
    - `handleSubmitResponse(response, postData)` — standard [success, message, newId] response handler
    - `getEditOptions(gridId, apiEndpoint, serializeFn)` — navGrid edit options (PUT)
    - `getAddOptions(gridId, apiEndpoint, serializeFn)` — navGrid add options (POST)
    - `getDeleteOptions(gridId, apiEndpoint)` — navGrid delete options (DELETE)
    - `getDefaultGridOptions()` — default grid config including `jsonReader` mapped to this project's response format:
      ```javascript
      jsonReader: {
          root: "data",
          page: "page",
          total: "totalPages",
          records: "totalCount",
          repeatitems: false,
          id: "id"
      }
      ```
    - `createSerializeGridData(gridId, state)` — creates `serializeGridData` function that maps jqGrid's `postData` to the controller's query params (`page`, `pageSize`, `sortBy`, `sortOrder`, `filters`)
    - `createLoadErrorHandler(entityName)` — standard error handler
    - `setupNavigation(gridId, pagerId, apiEndpoint, serializeFn)` — sets up navGrid with add/edit/delete/refresh buttons
    - `enableFilterToolbar(gridId)` — enables the filter toolbar with `searchOnEnter: false`, `stringResult: true`

    The file is a direct adaptation of the reference with NO changes to the logic — only the `jsonReader` mapping must match the controller's response property names (`data`, `totalCount`, `totalPages`, `page`, `pageSize`).

    **Key difference from reference**: The `onclickSubmit` in `getEditOptions` and `getDeleteOptions` must send the **Guid** row ID (not an int) in the URL. The reference code already uses `$(gridId).jqGrid('getGridParam', 'selrow')` which returns the string rowid — this works with Guid IDs without changes.

  - [x] 5.2 Copy the identical `gridUtils.js` file to `ClientService/wwwroot/js/gridUtils.js`. Both services use the same utility module.
  - [x] 5.3 **SCOPE GUARD**: Do NOT factor `gridUtils.js` into a shared npm package or shared build step. Simple file copy is appropriate for two services.

- [x] **Task 6: Create `measurementsGrid.js` for both services** (AC: #1, #2, #3)
  - [x] 6.1 Create `ServerService/wwwroot/js/grids/measurementsGrid.js` using the IIFE pattern from the reference `buildingsGrid.js`, adapted for this project's Measurement entity:
    ```javascript
    /**
     * Measurements Grid Configuration
     * Adapted from reference buildingsGrid.js pattern for Guid-based Measurement entity.
     */
    var MeasurementsGrid = (function() {
        "use strict";

        function initialize(gridId, pagerId) {
            var state = { lastPageSize: 10 };
            var apiEndpoint = "/api/v1/measurements-grid";

            var gridOptions = $.extend({}, GridUtils.getDefaultGridOptions(), {
                url: apiEndpoint + "/paged",
                colModel: [
                    {
                        label: "ID",
                        name: "id",
                        key: true,
                        width: 280,
                        align: "left",
                        sortable: true,
                        search: true,
                        searchoptions: {
                            sopt: ['eq', 'ne']
                        },
                        editable: false
                    },
                    {
                        label: "Value",
                        name: "value",
                        width: 100,
                        align: "right",
                        sortable: true,
                        search: true,
                        searchoptions: {
                            sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                        },
                        editable: true,
                        editrules: { required: true, number: true },
                        editoptions: { size: 10 },
                        formatter: "number",
                        formatoptions: { decimalPlaces: 2, thousandsSeparator: "" }
                    },
                    {
                        label: "Recorded At",
                        name: "recordedAt",
                        width: 160,
                        align: "center",
                        sortable: true,
                        search: true,
                        searchoptions: {
                            sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                        },
                        editable: true,
                        editrules: { required: true },
                        editoptions: { size: 20 }
                    },
                    {
                        label: "Synced At",
                        name: "syncedAt",
                        width: 160,
                        align: "center",
                        sortable: true,
                        search: true,
                        searchoptions: {
                            sopt: ['eq', 'ne', 'lt', 'le', 'gt', 'ge']
                        },
                        editable: false
                    },
                    {
                        label: "User ID",
                        name: "userId",
                        width: 280,
                        align: "left",
                        sortable: true,
                        search: true,
                        searchoptions: {
                            sopt: ['eq', 'ne']
                        },
                        editable: true,
                        editrules: { required: true },
                        editoptions: { size: 36 }
                    },
                    {
                        label: "Cell ID",
                        name: "cellId",
                        width: 280,
                        align: "left",
                        sortable: true,
                        search: true,
                        searchoptions: {
                            sopt: ['eq', 'ne']
                        },
                        editable: true,
                        editrules: { required: true },
                        editoptions: { size: 36 }
                    }
                ],
                pager: pagerId,
                sortname: "recordedAt",
                sortorder: "desc",
                caption: "Measurements",
                editurl: apiEndpoint,
                loadError: GridUtils.createLoadErrorHandler("measurements"),
                serializeGridData: GridUtils.createSerializeGridData(gridId, state),
                shrinkToFit: true,
                autowidth: true
            });

            $(gridId).jqGrid(gridOptions);

            // Enable resizable columns
            $(gridId).jqGrid('setGridWidth', $(gridId).closest('.ui-jqgrid').parent().width(), true);
            $(window).on('resize', function() {
                $(gridId).jqGrid('setGridWidth', $(gridId).closest('.ui-jqgrid').parent().width(), true);
            });

            // Enable filter toolbar
            GridUtils.enableFilterToolbar(gridId);

            // Setup navigation with CRUD buttons
            GridUtils.setupNavigation(
                gridId,
                pagerId,
                apiEndpoint,
                serializeEditData
            );
        }

        function serializeEditData(postData) {
            return JSON.stringify({
                value: parseFloat(postData.value),
                recordedAt: postData.recordedAt,
                userId: postData.userId,
                cellId: postData.cellId
            });
        }

        return {
            init: initialize
        };
    })();
    ```
  - [x] 6.2 Key differences from the reference `measurementsGrid.js`:
    - **Guid IDs** (not int): `id`, `userId`, `cellId` columns are wide (280px) and use `sopt: ['eq', 'ne']` (not numeric comparisons).
    - **This project's fields**: `value` (decimal), `recordedAt` (DateTime), `syncedAt` (DateTime?, read-only), `userId` (Guid), `cellId` (Guid). NOT alpha/beta/offsetX/offsetY from the reference.
    - **Default sort**: `sortname: "recordedAt"`, `sortorder: "desc"` (newest first — most useful for inspection).
    - **`syncedAt` is not editable**: It is set by the sync process, not manually.
    - **Responsive width**: Grid resizes on window resize via the `$(window).on('resize')` handler.
  - [x] 6.3 Copy the identical `measurementsGrid.js` to `ClientService/wwwroot/js/grids/measurementsGrid.js`. Both services use the same grid definition since the API endpoint path (`/api/v1/measurements-grid/paged`) and response shape are identical.
  - [x] 6.4 **SCOPE GUARD**: Do NOT create grid JS files for other entities (Buildings, Rooms, Surfaces, Cells, Users). This story is scoped to Measurements only. Future stories may add other entity grids to ServerService — they are not needed now.

- [x] **Task 7: Add Measurements grid view page to ServerService** (AC: #1, #3)
  - [x] 7.1 Create `ServerService/Views/Home/Measurements.cshtml`:
    ```html
    @{
        ViewData["Title"] = "Measurements";
    }

    <link rel="stylesheet" href="~/lib/jqueryui/jquery-ui.min.css" />
    <link rel="stylesheet" href="~/lib/free-jqgrid/css/ui.jqgrid.min.css" />

    <style>
        /* Custom grid selection colors */
        #measurementsGrid .ui-state-highlight,
        #measurementsGrid tr.ui-state-highlight td {
            background-color: #e8e8e8 !important;
            border-color: transparent !important;
        }
        #measurementsGrid .edit-cell {
            background-color: #e8e8e8 !important;
            border-color: transparent !important;
        }
    </style>

    <h2>Measurements</h2>
    <p>Inspect, sort, filter, and manage measurements on ServerService. Use the filter toolbar row to search by any column.</p>

    <table id="measurementsGrid"></table>
    <div id="measurementsGridPager"></div>

    @section Scripts {
        <script src="~/lib/free-jqgrid/js/jquery.jqgrid.min.js"></script>
        <script src="~/js/gridUtils.js" asp-append-version="true"></script>
        <script src="~/js/grids/measurementsGrid.js" asp-append-version="true"></script>
        <script>
            $(function () {
                "use strict";
                MeasurementsGrid.init("#measurementsGrid", "#measurementsGridPager");
            });
        </script>
    }
    ```
  - [x] 7.2 Add a `Measurements` action to `ServerService/Controllers/HomeController.cs`:
    ```csharp
    public IActionResult Measurements()
    {
        return View();
    }
    ```
  - [x] 7.3 The Measurements page is accessible at `/Home/Measurements` (conventional route). This keeps it separate from the admin/scenario home page and avoids cluttering `Index.cshtml`.
  - [x] 7.4 **jQuery UI CSS is loaded on this page only** (via `<link>` in the page, not in `_Layout.cshtml`). This prevents jQuery UI theme styles from leaking into the rest of the app. The jQuery UI JS is NOT needed — only the CSS theme is required for jqGrid styling. jQuery itself is already loaded from `_Layout.cshtml`.
  - [x] 7.5 The `@section Scripts` block ensures script references render at the bottom of the page (after `_Layout.cshtml`'s jQuery include), so `$` is available when `gridUtils.js` and `measurementsGrid.js` execute.
  - [x] 7.6 Run `dotnet build MicrosericesSync.sln` — 0 errors.

- [x] **Task 8: Add Measurements grid view page to ClientService** (AC: #2, #3)
  - [x] 8.1 Create `ClientService/Views/Home/Measurements.cshtml` with identical content to ServerService's version (Task 7.1), except the `<p>` description text should say "ClientService" instead of "ServerService":
    ```html
    <p>Inspect, sort, filter, and manage measurements on ClientService. Use the filter toolbar row to search by any column.</p>
    ```
  - [x] 8.2 Add a `Measurements` action to `ClientService/Controllers/HomeController.cs`:
    ```csharp
    public IActionResult Measurements()
    {
        return View();
    }
    ```
  - [x] 8.3 Run `dotnet build MicrosericesSync.sln` — 0 errors.

- [x] **Task 9: Add navigation links to Measurements grid from both services' home pages** (AC: #1, #2)
  - [x] 9.1 In `ServerService/Views/Shared/_Layout.cshtml`, add a nav link for Measurements. Insert a new `<li>` after the existing Privacy nav item:
    ```html
    <li class="nav-item">
        <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Measurements">Measurements</a>
    </li>
    ```
  - [x] 9.2 In `ClientService/Views/Shared/_Layout.cshtml`, add the same nav link:
    ```html
    <li class="nav-item">
        <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Measurements">Measurements</a>
    </li>
    ```
  - [x] 9.3 This adds "Measurements" to the top navigation bar on both services, making the grid accessible from any page. The link points to `/Home/Measurements`.
  - [x] 9.4 Run `dotnet build MicrosericesSync.sln` — 0 errors.

- [x] **Task 10: Write unit tests for `MeasurementsGridController` paged/filtered queries** (AC: #1, #2, #3)
  - [x] 10.1 Create `MicroservicesSync.Tests/MeasurementsGrid/MeasurementsGridPagingTests.cs`.
  - [x] 10.2 Use the same in-memory SQLite test pattern from Story 3.1:
    - Declare a `TestableServerDbContextForGrid` inner class (same `RowVersion` → `ValueGeneratedNever` override pattern).
    - Use `SqliteConnection` kept open + `DbContextOptions<ServerDbContext>` over SQLite.
    - Implement `IDisposable`.
    - Seed `Users` and `Cells` entries (required FKs for Measurement). Use stable GUIDs:
      - User 1: `00000000-0000-0000-0000-000000000001`
      - Cell 1: first Cell GUID from `DatabaseSeeder` (look up the actual value)
    - Seed a set of Measurement rows with varying `Value`, `RecordedAt`, `UserId`, `CellId` values for filtering/sorting tests.
  - [x] 10.3 Test `GetPaged_NoFilters_ReturnsFirstPage`:
    - Seed 15 measurements.
    - Call `GetPaged(page: 1, pageSize: 10)`.
    - Assert HTTP 200. Assert 10 `data` items returned. Assert `totalCount == 15`, `totalPages == 2`, `page == 1`.
  - [x] 10.4 Test `GetPaged_Page2_ReturnsRemainder`:
    - Seed 15 measurements.
    - Call `GetPaged(page: 2, pageSize: 10)`.
    - Assert HTTP 200. Assert 5 `data` items returned.
  - [x] 10.5 Test `GetPaged_FilterByUserId_ReturnsOnlyMatchingRows`:
    - Seed 10 measurements: 7 for User1, 3 for User2.
    - Build a `filters` JSON: `{"groupOp":"AND","rules":[{"field":"userId","op":"eq","data":"<user1-guid>"}]}`.
    - Call `GetPaged` with that filters string.
    - Assert 7 results returned.
  - [x] 10.6 Test `GetPaged_FilterByValueRange_ReturnsCorrectSubset`:
    - Seed measurements with Values: 1.0, 2.0, 3.0, 4.0, 5.0.
    - Filter: `value gt 2.0`.
    - Assert 3 results returned (3.0, 4.0, 5.0).
  - [x] 10.7 Test `GetPaged_SortByValueDesc_ReturnsDescending`:
    - Seed measurements with varying Values.
    - Call `GetPaged(sortBy: "value", sortOrder: "desc")`.
    - Assert first returned value >= second returned value.
  - [x] 10.8 Test `GetPaged_InvalidFilterJson_Returns400`:
    - Call `GetPaged(filters: "not-json")`.
    - Assert HTTP 400.
  - [x] 10.9 Run `dotnet build MicrosericesSync.sln` — 0 errors.
  - [x] 10.10 Run `dotnet test` — all existing tests pass; new tests pass.

- [x] **Task 11: Write unit tests for `JqGridHelper` filter expressions** (AC: #3)
  - [x] 11.1 Create `MicroservicesSync.Tests/MeasurementsGrid/JqGridHelperTests.cs`.
  - [x] 11.2 These tests operate on in-memory `List<T>.AsQueryable()` — no database needed. Define a simple test entity class within the test file:
    ```csharp
    private class TestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int Count { get; set; }
    }
    ```
  - [x] 11.3 Test `ApplyFilters_GuidEq_FiltersCorrectly`:
    - Create 3 items with different GUIDs.
    - Filter: `id eq <guid-of-item-2>`.
    - Assert 1 result with matching ID.
  - [x] 11.4 Test `ApplyFilters_DecimalGt_FiltersCorrectly`:
    - Create items with Values 1.0, 2.0, 3.0.
    - Filter: `value gt 1.5`.
    - Assert 2 results (2.0, 3.0).
  - [x] 11.5 Test `ApplyFilters_DateTimeGe_FiltersCorrectly`:
    - Create items with dates 2026-01-01, 2026-06-01, 2026-12-01.
    - Filter: `createdAt ge 2026-06-01`.
    - Assert 2 results.
  - [x] 11.6 Test `ApplyFilters_StringContains_FiltersCorrectly`:
    - Create items with names "Alpha", "Beta", "AlphaTwo".
    - Filter: `name cn Alpha`.
    - Assert 2 results.
  - [x] 11.7 Test `ApplyFilters_InvalidFieldName_IgnoredSafely`:
    - Filter with `field: "nonexistent"`.
    - Assert all items still returned (filter rule skipped).
  - [x] 11.8 Test `ApplyFilters_InvalidGuidData_IgnoredSafely`:
    - Filter: `id eq not-a-guid`.
    - Assert all items still returned (unparseable rule skipped).
  - [x] 11.9 Test `ApplySort_SortByValueDesc_SortsCorrectly`:
    - Create items with Values 3.0, 1.0, 2.0.
    - Sort: `sortBy: "value"`, `sortOrder: "desc"`.
    - Assert order: 3.0, 2.0, 1.0.
  - [x] 11.10 Run `dotnet test` — all tests pass.

- [x] **Task 12: Manual Docker smoke test** (AC: #1, #2, #3)
  - [x] 12.1 Run `docker-compose up --build -d` from `MicroservicesSync/`.
  - [x] 12.2 Open ServerService home page (`http://localhost:5100`). Confirm the new "Measurements" link appears in the top navigation bar.
  - [x] 12.3 Click "Measurements" nav link. Confirm the jqGrid loads with proper columns: ID, Value, Recorded At, Synced At, User ID, Cell ID.
  - [x] 12.4 If measurements exist (from a previous scenario run or after running generate → push): confirm pagination shows correct page counts, sorting works (click column headers), and the filter toolbar row appears above the data rows.
  - [x] 12.5 Test filtering: type a known User ID in the "User ID" filter box. Confirm only matching rows appear.
  - [x] 12.6 Test CRUD: use the Add (+) button to create a test measurement. Use Edit (pencil) to modify it. Use Delete (trash) to remove it. Confirm each operation refreshes the grid.
  - [x] 12.7 Open any ClientService instance (e.g., `http://localhost:5101`). Confirm the "Measurements" nav link appears. Click it. Confirm the identical jqGrid loads.
  - [x] 12.8 **Comparison test (AC#2)**: Run a full scenario (generate → push → pull on all clients). After pull completes:
    - On ServerService Measurements grid: sort by "User ID" ascending, note the visible rows.
    - On a ClientService Measurements grid: apply the same sort. Confirm the same measurement rows appear (same IDs, Values, timestamps).
  - [x] 12.9 **Resizability**: Resize the browser window. Confirm the grid width adjusts to fit the container.
  - [x] 12.10 Confirm existing functionality (Home page admin controls, Reset, Edge-Case Scenario, Sync Run Summary) still works on both services — no regressions.
  - [x] 12.11 Run `docker-compose down`.

## Dev Notes

### ⚠️ Story 3.2 — SIZE CHECK

This story spans both ServerService and ClientService with shared infrastructure work. Task breakdown:
- **Task 1**: LibMan setup (2 files + restore) — mechanical, low risk.
- **Task 2**: Shared `JqGridFilter` + `JqGridHelper` in `Sync.Infrastructure` — the most logic-heavy task, but it's a standalone static utility with comprehensive tests.
- **Tasks 3–4**: Two nearly identical controllers (one per service) — straightforward CRUD+paged endpoints following established patterns.
- **Tasks 5–6**: JavaScript files (shared utility + grid) — adapted from proven reference code.
- **Tasks 7–8**: Razor views — minimal markup.
- **Task 9**: Nav links — 2 lines each.
- **Tasks 10–11**: Unit tests — thorough coverage of filtering/paging/sorting.
- **Task 12**: Smoke test — manual verification.

**Assessment**: Moderate size. The work is repetitive (same patterns applied to two services) but not complex. The shared `JqGridHelper` is the only piece with real logic density, and it's well-scoped with dedicated tests.

### Architecture Constraints (MUST follow)

- **Direct DbContext injection**: Both `MeasurementsGridController` implementations inject their respective `DbContext` directly. No Application-layer service, no generic repository. Consistent with `AdminController`, `SyncMeasurementsController`, `SyncRunsController`.
- **No AutoMapper**: Anonymous projections in `Select()` are sufficient. The controller returns exactly what jqGrid needs — no mapping layer.
- **No shared web-layer DTOs**: `MeasurementCreateRequest` and `MeasurementUpdateRequest` are defined separately in each service's controller file. They are web-layer concerns and the two services ship independently.
- **`JqGridHelper` and `JqGridFilter` live in `Sync.Infrastructure`**: They are data-access utilities (build LINQ expressions) — appropriate for the Infrastructure layer. Both services already reference `Sync.Infrastructure`.
- **`gridUtils.js` and `measurementsGrid.js` are copied** to both services' `wwwroot/js/` folders. No shared package build step.
- **Measurements only**: This story creates grid infrastructure for Measurements only. Other entity grids (Buildings, Rooms, etc.) on ServerService are NOT in scope.
- **No existing controller route collisions**: The new `MeasurementsGridController` uses route `api/v1/measurements-grid` to avoid collision with ClientService's existing `MeasurementsController` at `api/v1/measurements`.

### What NOT to Do in This Story (Scope Guards)

- ❌ Do NOT create grids for any entity other than Measurement. Buildings, Rooms, Surfaces, Cells, and Users grids are NOT in scope.
- ❌ Do NOT create a generic repository (`IRepository<T>`) or generic service (`IGenericService<T>`) layer. Use direct DbContext queries.
- ❌ Do NOT add AutoMapper, AutoMapper profiles, or separate DTO folders.
- ❌ Do NOT modify `Sync.Domain/Entities/Measurement.cs` or any other domain entity.
- ❌ Do NOT modify `ServerDbContext` or `ClientDbContext`.
- ❌ Do NOT modify the existing `SyncMeasurementsController`, `SyncRunsController`, `AdminController`, or `MeasurementsController` (sync operations).
- ❌ Do NOT add authentication or authorization to the new endpoints.
- ❌ Do NOT add structured logging or correlation IDs — Story 3.4 owns this.
- ❌ Do NOT add jQuery via LibMan — it already exists in `wwwroot/lib/jquery/` from the ASP.NET Core template.
- ❌ Do NOT modify `docker-compose.yml` or `docker-compose.override.yml`.
- ❌ Do NOT put jqGrid CSS/JS references in `_Layout.cshtml` — they belong only on the Measurements page.

### `// NOTE` Deviation Pattern

This story adds one new acknowledged deviation (used in both services):
```csharp
// NOTE (M3.2): MeasurementsGridController injects ServerDbContext/ClientDbContext directly.
// Consistent with the acknowledged deviation in AdminController (NOTE M4),
// SyncMeasurementsController, and SyncRunsController (NOTE M3.1).
// Acceptable for this experiment scope.
```

### Security Notes

- **SQL injection prevention**: All database queries use EF Core LINQ — parameterized by default. The `JqGridHelper.ApplyFilters` method builds expression trees from filter rules and validates property names against `typeof(T).GetProperty()` — arbitrary property access is rejected (property must exist on the entity). Filter data values are parsed via `TryParse` for their respective types — no raw string concatenation into SQL.
- **XSS prevention**: jqGrid handles HTML encoding of cell values internally. The grid renders data returned from the JSON API — no raw HTML insertion.
- **Input validation**: `page` and `pageSize` are clamped to valid ranges. `filters` JSON is deserialized in a try/catch — malformed JSON returns 400.

### LibMan Installation Notes

If the `libman` CLI tool is not installed globally, install it first:
```powershell
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
```

Alternatively, if Visual Studio is being used, right-click the `libman.json` file → "Restore Client-Side Libraries". The LibMan integration in VS handles restoration automatically on build.

**Important**: The LibMan-restored files (`wwwroot/lib/jqueryui/`, `wwwroot/lib/free-jqgrid/`) should be committed to source control (they are not in `.gitignore` by default in ASP.NET Core projects). This ensures `docker-compose build` includes them without needing `libman restore` in the Dockerfile.

### jQuery Version Compatibility

The ASP.NET Core template ships jQuery 3.5.x or similar in `wwwroot/lib/jquery/`. jQuery 3.7.1 from the reference `libman.json` is a compatible upgrade but NOT required. The existing jQuery version is sufficient for free-jqGrid 4.15.5. If the user wants to upgrade jQuery to 3.7.1, add it as a separate follow-up task — do NOT replace the existing jQuery in this story.

### Stable GUIDs for Test Seeding

From `DatabaseSeeder.cs`:
- User 1: `00000000-0000-0000-0000-000000000001` (Username: "user1")
- User 2: `00000000-0000-0000-0000-000000000002` (Username: "user2")
- Building 1: `10000000-0000-0000-0000-000000000001`
- Room 1: `20000000-0000-0000-0000-000000000001` (BuildingId: Building 1)
- Surface 1: `30000000-0000-0000-0000-000000000001` (RoomId: Room 1)
- Cell 1: `40000000-0000-0000-0000-000000000001` (SurfaceId: Surface 1)

Tests must seed the full FK chain (User → Building → Room → Surface → Cell) before seeding Measurements.

### Test Pattern Reference

Use the EXACT same in-memory SQLite pattern from Story 3.1 tests:
```csharp
internal sealed class TestableServerDbContextForGrid : ServerDbContext
{
    public TestableServerDbContextForGrid(DbContextOptions<ServerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty("RowVersion");
            if (rowVersionProp != null)
                modelBuilder.Entity(entityType.ClrType)
                    .Property("RowVersion")
                    .ValueGeneratedNever();
        }
    }
}
```

### Project Structure — Files to Create

**Shared (Sync.Infrastructure):**
- `Sync.Infrastructure/Grid/JqGridFilter.cs` — filter model (3 classes)
- `Sync.Infrastructure/Grid/JqGridHelper.cs` — static filter/sort expression builder

**ServerService:**
- `ServerService/libman.json` — LibMan config (jqueryui + free-jqgrid)
- `ServerService/wwwroot/js/gridUtils.js` — shared grid utility IIFE module
- `ServerService/wwwroot/js/grids/measurementsGrid.js` — measurements grid IIFE module
- `ServerService/Controllers/MeasurementsGridController.cs` — paged CRUD API controller
- `ServerService/Views/Home/Measurements.cshtml` — grid view page

**ClientService:**
- `ClientService/libman.json` — LibMan config (identical to ServerService's)
- `ClientService/wwwroot/js/gridUtils.js` — shared grid utility IIFE module (copy)
- `ClientService/wwwroot/js/grids/measurementsGrid.js` — measurements grid IIFE module (copy)
- `ClientService/Controllers/MeasurementsGridController.cs` — paged CRUD API controller
- `ClientService/Views/Home/Measurements.cshtml` — grid view page

**Tests:**
- `MicroservicesSync.Tests/MeasurementsGrid/MeasurementsGridPagingTests.cs` — controller paging/filtering tests
- `MicroservicesSync.Tests/MeasurementsGrid/JqGridHelperTests.cs` — helper expression builder tests

### Files to Modify

- `ServerService/Controllers/HomeController.cs` — add `Measurements()` action
- `ClientService/Controllers/HomeController.cs` — add `Measurements()` action
- `ServerService/Views/Shared/_Layout.cshtml` — add Measurements nav link
- `ClientService/Views/Shared/_Layout.cshtml` — add Measurements nav link

### Files NOT to Modify

- `Sync.Domain/Entities/Measurement.cs` — no entity changes
- `Sync.Infrastructure/Data/ServerDbContext.cs` — no context changes
- `Sync.Infrastructure/Data/ClientDbContext.cs` — no context changes
- Any existing controller (`AdminController`, `SyncMeasurementsController`, `SyncRunsController`, `MeasurementsController`)
- Any existing test file
- `docker-compose.yml`, `docker-compose.override.yml`
- `ServerService/Program.cs`, `ClientService/Program.cs`
- Any `.csproj` files (no new NuGet packages needed — `System.Text.Json` and EF Core are already referenced)

### References

- Story requirement (AC#1, AC#2, AC#3): [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.2`]
- FR11 (inspect measurement data via jqGrid): [Source: `_bmad-output/planning-artifacts/prd.md`]
- Reference jqGrid pattern — libman.json: [Source: `docs/code-snippets/libman.json`]
- Reference jqGrid pattern — gridUtils.js: [Source: `docs/code-snippets/wwwroot/js/gridUtils.js`]
- Reference jqGrid pattern — buildingsGrid.js: [Source: `docs/code-snippets/wwwroot/js/grids/buildingsGrid.js`]
- Reference jqGrid pattern — measurementsGrid.js: [Source: `docs/code-snippets/wwwroot/js/grids/measurementsGrid.js`]
- Reference controller — MeasurementController.cs: [Source: `docs/code-snippets/Controllers/MeasurementController.cs`]
- Reference repository — Repository.cs (filter expression builder): [Source: `docs/code-snippets/Repositories/Repository.cs`]
- Reference model — JqGridFilter.cs: [Source: `docs/code-snippets/Models/Grid/JqGridFilter.cs`]
- Reference view — Views/Home/Index.cshtml (grid layout, CSS, script includes): [Source: `docs/code-snippets/Views/Home/Index.cshtml`]
- Existing ServerService controllers (established patterns): [Source: `MicroservicesSync/ServerService/Controllers/`]
- Existing ClientService controllers (established patterns): [Source: `MicroservicesSync/ClientService/Controllers/`]
- Measurement entity: [Source: `MicroservicesSync/Sync.Domain/Entities/Measurement.cs`]
- ServerDbContext: [Source: `MicroservicesSync/Sync.Infrastructure/Data/ServerDbContext.cs`]
- ClientDbContext: [Source: `MicroservicesSync/Sync.Infrastructure/Data/ClientDbContext.cs`]
- Layout files: [Source: `MicroservicesSync/ServerService/Views/Shared/_Layout.cshtml`], [Source: `MicroservicesSync/ClientService/Views/Shared/_Layout.cshtml`]
- Story 3.1 (format reference, test patterns): [Source: `_bmad-output/implementation-artifacts/3-1-server-side-sync-run-summary-view.md`]
- Sprint status: [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml`]
