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