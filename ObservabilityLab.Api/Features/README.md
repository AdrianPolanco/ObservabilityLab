# Features — Vertical Slice Template

Each feature lives entirely in one file: request, response, handler, and endpoint
registration together. No AutoMapper, no base classes, no magic.

---

## Folder layout

```
Features/
  Common/
    ResultExtensions.cs          ← shared Result<T> → IResult helpers (do not edit per-feature)
  <Entity>/
    <Entity>Endpoints.cs         ← MapGroup + per-slice wiring for this entity
    <UseCase>/
      <UseCase>.cs               ← the full vertical slice (request + handler + response + mapping)
```

**Example — the `CreateOrder` slice:**

```
Features/
  Orders/
    OrderEndpoints.cs
    Create/
      CreateOrder.cs
```

---

## How to add a new use-case

### 1. Copy an existing slice and rename it

```
cp -r Features/Orders/Create Features/Orders/GetById
```

Inside `GetById/GetById.cs` (rename the file too), look for the 4 `TEMPLATE` comments:

| Point | What to change |
|-------|----------------|
| Class name | `CreateOrder` → `GetOrderById` |
| Request/Response | match the new operation |
| Factory / query | `Order.Create(...)` → `db.Orders.FindAsync(id, ct)` |
| Route + verb | `MapPost("/")` → `MapGet("/{id:guid}")` |

### 2. Register the slice in the entity's `*Endpoints` class

```csharp
// Features/Orders/OrderEndpoints.cs
GetOrderById.MapEndpoint(group);
```

### 3. Register the entity group in `Program.cs` (once per entity, already done for Orders)

```csharp
app.MapOrderEndpoints();
```

---

## How `Result<T>` maps to HTTP

`ResultExtensions` (in `Features/Common/`) provides two helpers:

| Helper | Usage | HTTP outcome |
|--------|-------|--------------|
| `result.ToProblem()` | explicit failure branch | `400 Bad Request` with `IReadOnlyList<Error>` |
| `result.Match(onSuccess)` | combined success/failure | calls `onSuccess` or falls back to `ToProblem()` |

`Error` is `record(string Code, string Message)` — clients always get structured codes.

**Typical handler skeleton:**

```csharp
private static async Task<IResult> Handle(Request req, ApplicationDbContext db, CancellationToken ct)
{
    var result = MyEntity.Create(req.SomeField);
    if (!result.IsSuccess) return result.ToProblem();

    db.MyEntities.Add(result.Data!);
    await db.SaveChangesAsync(ct);

    return TypedResults.Created($"/my-entities/{result.Data!.Id}", ToResponse(result.Data!));
}
```
