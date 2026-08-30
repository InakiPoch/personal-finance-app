# C# Code Style

## Naming Conventions

- Helper classes must end with the word `Helper` (e.g., `ValidationHelper`, `ErrorResultsHelper`)
- Constants for centralized configuration use **sealed record types** as logical grouping containers
- **`private` members use `camelCase`** — fields, methods, properties and local variables. No `_` prefix, no PascalCase. When a private field collides with a constructor or method parameter of the same name, disambiguate with `this.` (e.g. `this.value = value;`). `public`/`protected`/`internal` members keep PascalCase.

```csharp
public sealed class Result<TValue> : Result {
    public TValue Value => IsSuccess ? value : throw new InvalidOperationException("...");

    private readonly TValue value;

    internal Result(TValue value, bool isSuccess, Error error) : base(isSuccess, error) {
        this.value = value;
    }

    private static long pow10(byte exponent) {
        // ...
    }
}
```

## API Route Versioning

- Every HTTP route is served under a version segment. `ApiRoutes` (host `Endpoints/`) exposes
  `public const string V1 = "/v1"`; each feature's `Base` const is **composed** from it —
  `public const string Base = V1 + "/ledger"` — so a feature group's prefix is absolute and the
  version is single-sourced.
- Endpoint-level route templates (`Transactions`, `AccountBalance`, …) stay **relative** to their
  feature `Base` and carry no version segment.
- A new API version is a new `V2` const plus new feature records. Never mutate `V1` or an existing
  feature `Base` — old routes keep working while `/v2/...` is added alongside.

## Extension Methods

Before adding an extension method, check whether another extension already targets the **exact same
receiver** — same type **and** same parameter name (`Guid transactionId` and `Guid accountId` are
different receivers).

- **Another extension for that receiver already exists** → encapsulate every extension for it under a
  single `extension(T name) { ... }` block (C# 14 extension members). Move the pre-existing one in too.
- **No other extension for that receiver** → declare it individually, as always:
  `public static {Ret} To...(this T name) { ... }`.

Never leave a lone extension wrapped in an `extension(...)` block, and never leave two extensions for
the same receiver as separate `this`-parameter methods.

```csharp
// One extension for (Guid transactionId) → individual form
public static PostTransactionResultDto ToPostTransactionResultDto(this Guid transactionId) {
    return new PostTransactionResultDto(transactionId);
}

// Two+ extensions for (ReverseTransactionResult result) → grouped
extension(ReverseTransactionResult result) {
    public ReverseTransactionResultDto ToReverseTransactionResultDto(Guid originalTransactionId) {
        return new ReverseTransactionResultDto(result.ReversalTransactionId, originalTransactionId, result.CompensatingEntryPosted);
    }

    public bool IsNoop() {
        return !result.CompensatingEntryPosted;
    }
}
```

## Formatting

- No space between keyword and parenthesis: `if()`, `while()`, `for()`, `foreach()`, `switch()`
- Opening curly braces on the same line as the statement, not the line below

```csharp
// Correct
if(condition) {
    ...
}

public static IResult Handle(AppDbContext context) {
    ...
}

// Wrong
if (condition)
{
    ...
}

public static IResult Handle(AppDbContext context)
{
    ...
}
```