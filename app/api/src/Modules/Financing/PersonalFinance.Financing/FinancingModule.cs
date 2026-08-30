using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Modularity;

namespace PersonalFinance.Financing;

public sealed class FinancingModule : IModule {
    public string Name => "Financing";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        // Wired in Step 14 — DbContext, facade, handlers, scheduler.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // HTTP surface is host-owned (src/Bootstrap/PersonalFinance.Api/Endpoints/) — see LedgerModule.
    }
}
