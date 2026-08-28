var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if(app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Temporary liveness probe — replaced by the real health check in Phase 8.
app.MapGet("/health", () => Results.Ok());

app.Run();
