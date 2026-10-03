using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PersonalFinance.Api;
using PersonalFinance.Api.Endpoints;
using PersonalFinance.Api.Handlers;
using PersonalFinance.Api.Helpers;
using PersonalFinance.Api.OpenApi;
using PersonalFinance.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiDocumentInfoTransformer>());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var corsOptions = builder.Configuration.GetSection(ClientCorsOptions.SectionName).Get<ClientCorsOptions>() ?? new ClientCorsOptions();
builder.Services.AddCors(options => {
    options.AddPolicy(ClientCorsOptions.PolicyName, policy => {
        if(corsOptions.AllowedOrigins.Length > 0) {
            policy.WithOrigins(corsOptions.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddSharedInfrastructure(builder.Configuration);

builder.Services.AddModules(builder.Configuration);

builder.Services.AddOutboxProcessing();

var migrateOnStartup = builder.Configuration.GetValue<bool>("Database:MigrateOnStartup");
var contextTypes = migrateOnStartup ? DatabaseMigrationHelper.GetOrderedContextTypes(builder.Services) : [];

var app = builder.Build();

app.UseExceptionHandler();

// Always served — CI and client codegen pull the spec outside Development too.
app.MapOpenApi();

if(app.Environment.IsDevelopment()) {
    // Scalar API reference UI with a built-in request client — https://scalar.com/#api-reference
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors(ClientCorsOptions.PolicyName);

app.MapModuleEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions {
    ResponseWriter = HealthCheckResponseWriterHelper.Write
});

// A mistyped API path must stay a 404.
app.MapFallback("/v1/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

if(migrateOnStartup) {
    await DatabaseMigrationHelper.MigrateAsync(app.Services, contextTypes, app.Logger);
}

var publicUrl = app.Configuration["App:PublicUrl"];
if(!string.IsNullOrWhiteSpace(publicUrl)) {
    app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation("Personal Finance is ready at {Url}", publicUrl));
}

app.Run();
