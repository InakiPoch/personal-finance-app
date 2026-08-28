# Minimal API Structure

A transferable blueprint for structuring an ASP.NET Core Minimal API. It defines
**folder layout**, **file layout**, **naming**, and **member organization**.
Replace every `{Placeholder}` with a real name; the shape stays constant.

---

## 1. Folder Structure

All application code lives under a single source folder (e.g. `src/`), split by
**technical role**, then by **feature** inside the routing layer.

```
src/
├── Models/                     Plain domain classes (no framework attributes beyond persistence needs)
├── Data/
│   ├── {AppDbContext}.cs       EF Core context
│   ├── Migrations/             Generated migrations (never hand-edited)
│   └── Repositories/           Repository implementations (one file per aggregate)
├── Contracts/                  Interfaces only
│   ├── Repositories/           I{Feature}Repository
│   └── Services/               I{Name}Service
├── Routes/
│   ├── {UrlConstants}.cs       Centralized route URL constants (see §3)
│   ├── {RouteGroupExtensions}.cs  Wires every feature group + cross-cutting config
│   └── {Feature}/              One folder per feature/resource
│       ├── {Feature}EndpointsGroup.cs   Maps this feature's endpoints
│       ├── Endpoints/          One file = one endpoint (static class, static Handle)
│       └── DTOs/               Request/response DTOs scoped to this feature
├── Services/                   DI-registered services (business logic, integrations)
├── Extensions/                 Model → DTO mapping extension methods (one file per model)
├── Helpers/
│   ├── DTOs/                   Records used internally by helpers (paging, config, results)
│   └── {Name}Helper.cs         Static utility classes
├── Handlers/                   IExceptionHandler implementations
├── Filters/                    IEndpointFilter implementations (cross-cutting endpoint concerns)
└── Program.cs                  Composition root only: DI registration + pipeline + route mapping
```

**Rules**

- A feature never spreads outside its `Routes/{Feature}/` folder except for: its
  model (`Models/`), its repository (`Data/Repositories/` + `Contracts/`), and its
  mapping extension (`Extensions/`).
- `Program.cs` contains **no business logic** — only registration and wiring.
- Route DTOs go in `Routes/{Feature}/DTOs/`. DTOs consumed only by helpers go in
  `Helpers/DTOs/`.
- Repositories depend on `Contracts/` interfaces, never on concrete siblings.

---

## 2. File Structure

### Endpoint handler (`Routes/{Feature}/Endpoints/{Verb}{Action}.cs`)

- One **static class** per endpoint, named for the HTTP verb + action
  (`Get{Resource}`, `Post{Resource}`, `Put{Resource}`, `Delete{Resource}`).
- Exactly one `public static` method named `Handle`.
- Dependencies arrive as method parameters (DI-injected), not constructor fields.
- Returns a typed result union (see §4), never `IResult` with hidden branches.

```csharp
public static class Get{Resource} {
    public static async Task<Results<Ok<{Get{Resource}ByIdDto}>, NotFound>> Handle(
        string id,
        I{Feature}Repository repository) {
        var entity = await repository.Get{Resource}Async(id);
        if(entity is null) return {ProblemResults}.NotFound($"...");
        return TypedResults.Ok(entity.To{Resource}ByIdDto());
    }
}
```

### Feature endpoints group (`Routes/{Feature}/{Feature}EndpointsGroup.cs`)

- One extension method: `Map{Feature}Endpoints(this RouteGroupBuilder group)`.
- Maps each endpoint via the **URL constants file**, never a hardcoded string.
- Returns the `RouteGroupBuilder` for chaining.

```csharp
public static class {Feature}EndpointsGroup {
    public static RouteGroupBuilder Map{Feature}Endpoints(this RouteGroupBuilder group) {
        group.MapGet({UrlConstants}.{Feature}.GetAll, Get{Feature}s.Handle);
        group.MapPost({UrlConstants}.{Feature}.Create, Post{Feature}.Handle);
        return group;
    }
}
```

### Central route registration (`Routes/{RouteGroupExtensions}.cs`)

- Single `MapApiRoutes(this WebApplication app)` (or `IEndpointRouteBuilder`).
- One `MapGroup({UrlConstants}.{Feature}.Base)` per feature, then chained
  `.Map{Feature}Endpoints()`, `.RequireAuthorization()`, `.AddEndpointFilter<T>()`.

```csharp
app.MapGroup({UrlConstants}.{Feature}.Base)
   .Map{Feature}Endpoints()
   .RequireAuthorization()
   .AddEndpointFilter<{Name}Filter>();
```

### URL constants (`Routes/{UrlConstants}.cs`)

- One outer `static`/`sealed` type, one **nested `sealed record`** per feature.
- `Base` holds the absolute prefix; other members hold relative segments.

```csharp
public sealed record {UrlConstants} {
    public sealed record {Feature} {
        public const string Base = "/api/{feature}";
        public const string GetAll = "/";
        public const string GetById = "/{id}";
        public const string Create = "/create";
    }
}
```

### DTO mapping extension (`Extensions/{Model}Extensions.cs`)

- One static class per model. Method names are **DTO-specific**, never `ToDto()`.
- `To{Resource}ByIdDto()`, `To{Resource}sDto()` (list/collection shape), etc.
- Repositories call these inside `.Select(x => x.To{Resource}sDto())` projections.

### Helper (`Helpers/{Name}Helper.cs`)

- Class name **must** end in `Helper`. Static methods only. No state.

---

## 3. Naming Conventions

| Element | Convention | Example |
| --- | --- | --- |
| Endpoint class | `{HttpVerb}{Action}` | `PostCreateOrder` |
| Endpoint method | always `Handle` | `Handle` |
| Feature group method | `Map{Feature}Endpoints` | `MapOrdersEndpoints` |
| Repository interface | `I{Feature}Repository` | `IOrderRepository` |
| Service interface | `I{Name}Service` | `ITokenService` |
| Helper class | `{Name}Helper` (suffix required) | `PaginationHelper` |
| Mapping extension method | `To{DtoPurpose}Dto` (DTO-specific) | `ToOrderByIdDto` |
| Config/URL constant container | nested `sealed record` | `ApiUrls.Orders` |
| DTO type name | `…Dto` — only the **D** capitalized | `GetOrderByIdDto` |
| DTO file name | `…DTO.cs` — **all caps** for DTO | `GetOrderByIdDTO.cs` |
| Request DTO | `{HttpVerb}{Action}Dto` | `PostCreateOrderDto` |
| Detail response DTO | `Get{Resource}ByIdDto` | `GetOrderByIdDto` |
| List response DTO | `Get{Resource}sDto` (lighter payload) | `GetOrdersDto` |
| Filter | `{Name}Filter` implementing `IEndpointFilter` | `LogUserActivity` |
| Exception handler | `{Reason}ExceptionHandler` | `ValidationExceptionHandler` |
| Params record (paging/filter) | `{Feature}Params` deriving a shared base | `OrderParams : PagingParams` |

### Namespaces

- Route DTOs: `{RootNamespace}.Routes.{Feature}.DTOs`
- Helper DTOs: `{RootNamespace}.Helpers.DTOs`
- Mirror the folder path in the namespace everywhere else.

---

## 4. Organization & Patterns

### Formatting

- No space between keyword and parenthesis: `if()`, `for()`, `foreach()`, `while()`, `switch()`.
- Opening brace on the **same line** as the statement.

### Typed results over exceptions

Endpoints declare failure in the signature via `Results<Ok<T>, NotFound, …>` /
`Results<T1, T2>` unions. Normal failure paths `return` a typed result:

- Validation → `TypedResults.ValidationProblem(errors.ToProblemErrors())`
- Not found / unauthorized / forbidden → shared `{ProblemResults}` helper
- Domain rejection → `TypedResults.BadRequest<T>(error)`

Exception handlers exist **only** as a last-resort fallback, registered in
`Program.cs` from most specific to catch-all.

### DTO construction

Always through a mapping extension method. **Never** `new SomeDto { … }` inside an
endpoint or repository.

### Endpoint filters

Registered as scoped services in `Program.cs`; applied to the **route group** with
`.AddEndpointFilter<T>()`, not to individual endpoints.

### Pagination (when applicable)

`{Feature}Params : PagingParams` → validated by a shared
`PagingParamsValidator<T>` → repository builds the query and calls a shared
`PaginationHelper.CreatePageAsync<T>(query, page, size)` → returns a
`PaginatedResult<T>` (metadata record + `Items`) in the response body.

### Law of Demeter

Models encapsulate related state changes as **behavior methods** (e.g.
`entity.UpdateName(value)` keeps two fields in sync). Endpoints do not chain
through the object graph (`entity.Owner.Profile.Name = …`).

### Class member order

Per `method-organization.md`: fields/properties (public → protected → private) →
constructors → public methods → protected → private. Applies to every class here.
