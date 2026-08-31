using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Reporting.Dashboards;

namespace PersonalFinance.Reporting;

public sealed class ReportingModule : IModule {
    public string Name => "Reporting";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddSingleton<IReadDbConnectionFactory, ReadDbConnectionFactory>();
        services.AddScoped<IQueryHandler<MonthlyExpensesQuery, MonthlyExpensesResponse>, MonthlyExpensesHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // HTTP surface is host-owned (src/Bootstrap/PersonalFinance.Api/Endpoints/) — see LedgerModule.
    }
}
