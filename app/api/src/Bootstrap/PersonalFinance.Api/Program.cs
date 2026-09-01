using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PersonalFinance.Api;
using PersonalFinance.Api.Endpoints;
using PersonalFinance.Api.Handlers;
using PersonalFinance.Infrastructure.DependencyInjection;
using PersonalFinance.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors(ClientCorsOptions.PolicyName);

app.MapModuleEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions {
    ResponseWriter = HealthCheckResponseWriterHelper.Write
});

app.Run();
