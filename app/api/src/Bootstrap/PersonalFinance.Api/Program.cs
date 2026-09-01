using PersonalFinance.Api;
using PersonalFinance.Api.Handlers;
using PersonalFinance.Infrastructure.DependencyInjection;
using PersonalFinance.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// One RFC-9457 ProblemDetails envelope for every failure — framework 400/404/405/415 and the
// last-resort GlobalExceptionHandler alike.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSharedInfrastructure(builder.Configuration);

builder.Services.PostConfigure<SqliteOptions>(options => {
    if(string.IsNullOrWhiteSpace(options.ConnectionString)) {
        options.ConnectionString = $"Data Source={Path.Combine(SolutionRootLocatorHelper.FindSolutionRoot(), "personalfinance.db")}";
    }
});

builder.Services.AddModules(builder.Configuration);

builder.Services.AddOutboxProcessing();

var app = builder.Build();

app.UseExceptionHandler();

if(app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    // Scalar API reference UI with a built-in request client — https://scalar.com/#api-reference
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapModuleEndpoints();

// Temporary liveness probe — replaced by the real health check in Phase 8.
app.MapGet("/health", () => Results.Ok());

app.Run();
