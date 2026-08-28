# C# Code Style

## Naming Conventions

- Helper classes must end with the word `Helper` (e.g., `ValidationHelper`, `ErrorResultsHelper`)
- Constants for centralized configuration use **sealed record types** as logical grouping containers (e.g., `ApiUrls.cs` with nested sealed records `Users` and `Members`)

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

## DTO Organization

**Folder structure:**
- Route-specific DTOs (API requests/responses) go in `API/src/Routes/{Feature}/DTOs/` (e.g., `API/src/Routes/Members/DTOs/`)
- Helper DTOs (used internally by helper classes) go in `API/src/Helpers/DTOs/` (e.g., pagination records, config records)

**Namespace pattern:**
- Route DTOs: `namespace API.Routes.{Feature}.DTOs;` (e.g., `API.Routes.Members.DTOs`)
- Helper DTOs: `namespace API.Helpers.DTOs;`

**DTO Naming Convention:**
- **Record/Class name**: `GetMessageByIdDto` — only the **D** in "Dto" is capitalized (e.g., `PostCreateMessageDto`, `GetMembersDto`)
- **File name**: `GetMessageByIdDTO.cs` — all caps for "DTO" (e.g., `PostCreateMessageDTO.cs`, `GetMembersDTO.cs`)

## DTO Extension Methods

- All model-to-DTO mapping **must** use extension methods in `API/src/Extensions/`, never direct `new DTO { ... }` instantiation
- Extension method names **must** be DTO-specific and reflect the DTO type being returned:
  - `ToMemberByIdDto()` (not `ToDto()`) for mapping a single `Member` to `GetMemberByIdDto`
  - `ToMembersDto()` for mapping a `Member` to `GetMembersDto` (list/collection DTO)
  - `ToPhotoByIdDto()` for mapping a single `Photo` to `GetPhotoByIdDto`
  - `ToMemberPhotosDto()` for mapping a `Photo` to `GetMemberPhotosDto` (list context)
  - `ToUserByIdDto(this User user, ITokenService tokenService)` — method names include any injected dependencies' purpose if needed in the signature

```csharp
// Correct: DTO-specific method name reflecting the return type
public static class MemberExtensions {
    public static GetMemberByIdDto ToMemberByIdDto(this Member member) { ... }
    public static GetMembersDto ToMembersDto(this Member member) { ... }
}

// Wrong: Generic ToDto() name when a model maps to multiple DTO types
public static class MemberExtensions {
    public static GetMemberByIdDto ToDto(this Member member) { ... }  // Ambiguous: which DTO?
}
```

## Endpoint Routing

- All endpoint route URLs are defined as constants in `API/src/Routes/ApiUrls.cs` (sealed records with nested groupings)
- Reference ApiUrls constants in `{Feature}EndpointsGroup.cs` files when mapping routes:

```csharp
// Correct: Use ApiUrls constants
public static RouteGroupBuilder MapMembersEndpoints(this RouteGroupBuilder group) {
    group.MapGet(ApiUrls.Members.GetAll, GetMembers.Handle);
    group.MapPost(ApiUrls.Members.UploadPhoto, PostUploadPhoto.Handle);
    return group;
}

// Wrong: Hardcoded paths
public static RouteGroupBuilder MapMembersEndpoints(this RouteGroupBuilder group) {
    group.MapGet("/", GetMembers.Handle);
    group.MapPost("/upload-photo", PostUploadPhoto.Handle);
    return group;
}
```

## Endpoint Filters

- Filters are registered as scoped services in `Program.cs` and applied to route groups in `RouteGroupBuilderExtensions.cs`
- Implement `IEndpointFilter` and add to the `Filters/` folder
- Apply filters via `.AddEndpointFilter<T>()` on the route group builder, not individual endpoints

```csharp
// In Program.cs: register filter as service
builder.Services.AddScoped<LogUserActivity>();

// In RouteGroupBuilderExtensions.cs: apply to route group
app.MapGroup(ApiUrls.Members.Base)
    .MapMembersEndpoints()
    .RequireAuthorization()
    .AddEndpointFilter<LogUserActivity>();
```
