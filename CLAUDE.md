# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 learning lab for observability across a small distributed system: one ASP.NET Core minimal API and three background-worker services, all sharing a single PostgreSQL database and domain model. The order → invoice → email pipeline is the intended domain flow, but the three workers (`OrderProcessingWorker`, `InvoiceWorker`, `EmailWorker`) are currently default `BackgroundService` scaffolds — their `Worker.cs` only logs a heartbeat. The real business logic still needs to be built on top of the shared domain in `ObservabilityLab.Shared`.

## Solution layout

Solution file is `ObservabilityLab.slnx` (the new XML solution format, not `.sln`).

- `ObservabilityLab.Api` — ASP.NET Core minimal API (`Microsoft.NET.Sdk.Web`). Still has the template `/weatherforecast` endpoint. OpenAPI is enabled in Development.
- `ObservabilityLab.OrderProcessingWorker` / `InvoiceWorker` / `EmailWorker` — worker services (`Microsoft.NET.Sdk.Worker`), each a `Host.CreateApplicationBuilder` + `AddHostedService<Worker>()`.
- `ObservabilityLab.Shared` — the only project with domain logic: entities, EF Core `DbContext`, entity configurations, the `Result<T>` type, and the DI extension. Every other project references it and calls `AddSharedDatabase`.

Dependency direction: `Api` / `*Worker` → `Shared`. `Shared` references nothing but `Npgsql.EntityFrameworkCore.PostgreSQL`. Keep new infrastructure-free domain code in `Shared`.

## Commands

```bash
docker compose up -d                                          # start PostgreSQL 15 (port 5432, db ObservabilityLab)
dotnet build                                                  # build the whole solution
dotnet run --project ObservabilityLab.Api                     # run the API
dotnet run --project ObservabilityLab.OrderProcessingWorker   # run a worker (same for Invoice/Email)
dotnet format                                                 # format
```

There is **no test project yet**. If you add one, prefer xUnit and wire it into `ObservabilityLab.slnx`.

### Database / EF Core

The model is **code-first via `IEntityTypeConfiguration`** (`OnModelCreating` calls `ApplyConfigurationsFromAssembly`), but **no migrations have been created yet** — there is no `Migrations/` folder. The DB is expected to already match the model. To start using migrations, the design package (`Microsoft.EntityFrameworkCore.Design`) is already referenced by the API and worker startup projects:

```bash
dotnet ef migrations add <Name> --project ObservabilityLab.Shared --startup-project ObservabilityLab.Api
dotnet ef database update --project ObservabilityLab.Shared --startup-project ObservabilityLab.Api
```

Connection string lives under `ConnectionStrings:DefaultConnection` in each startup project's `appsettings.json` and matches the `docker-compose.yml` credentials (`postgres`/`postgres`).

## Domain conventions (in `ObservabilityLab.Shared`)

These patterns are consistent across the entities — follow them when adding domain code:

- **Result pattern.** Operations that can fail return `Result<T>` (`Result.Success(data)` / `Result.Failure(error|errors)`), never exceptions for flow control. `Error` is a `record (string Code, string Message)`. Note `Result<T>` constrains `T : class`, so it does not wrap value types directly.
- **Encapsulated aggregates.** Entities have `private` constructors and are built through static factory methods (`Order.Create`, `OrderItem.Create`, `Invoice.Create`) that validate first and return a `Result<T>`. Setters are `private set`; collections are exposed as `IReadOnlyList<T>` over a private backing list (e.g. `Order.Items`). State changes go through methods (`AddItem`, `Place`, `UpdateStatus`, `MarkEmailAsSent`) that enforce invariants such as the order status transitions in `OrderStatus`.
- **`Entity` base** supplies a client-generated `Guid Id`.
- **EF mapping is explicit and lives in `Database/Configurations/`**, one `IEntityTypeConfiguration` per entity. Tables and columns use **snake_case** (`orders`, `customer_id`); the `OrderStatus` enum is persisted as a string via `HasConversion<string>()`. Relationships use shadow/explicit FKs (e.g. `HasForeignKey("OrderId")`). Add a configuration class for any new entity rather than annotating with attributes — it is auto-discovered by assembly scan.

## Notable current state

- The API and all three workers are largely unmodified `dotnet new` templates aside from the `AddSharedDatabase` wiring; expect to replace the scaffold endpoints/heartbeat loops when implementing real behavior.
- "Observability" (logging/metrics/tracing beyond the default `ILogger`) is the apparent goal of the lab but is not yet set up — there is no OpenTelemetry/exporter configuration in place.
