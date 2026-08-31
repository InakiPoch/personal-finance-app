using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Modularity;

namespace PersonalFinance.Reporting;

public sealed class ReportingModule : IModule {
    public string Name => "Reporting";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        // No services yet — populated as the read queries land (Steps 2–6).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // HTTP surface is host-owned (src/Bootstrap/PersonalFinance.Api/Endpoints/) — see LedgerModule.
    }
}
