using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Modularity;

namespace PersonalFinance.Reporting;

public sealed class ReportingModule : IModule {
    public string Name => "Reporting";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddSingleton<IReadDbConnectionFactory, ReadDbConnectionFactory>();
        // Query handlers are registered as they land (Steps 3–6).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // HTTP surface is host-owned (src/Bootstrap/PersonalFinance.Api/Endpoints/) — see LedgerModule.
    }
}
