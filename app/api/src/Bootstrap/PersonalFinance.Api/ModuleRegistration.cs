using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Api.Endpoints;
using PersonalFinance.Financing;
using PersonalFinance.Ledger;
using PersonalFinance.Parties;
using PersonalFinance.Reporting;
using PersonalFinance.Subscriptions;

namespace PersonalFinance.Api;

internal static class ModuleRegistration {
    private static readonly IModule[] modules = [new LedgerModule(), new FinancingModule(), new SubscriptionsModule(), new PartiesModule(), new ReportingModule()];

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration) {
        foreach(var module in modules) {
            module.Register(services, configuration);
        }
        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder endpoints) {
        foreach(var module in modules) {
            module.MapEndpoints(endpoints);
        }
        endpoints.MapLedgerEndpoints();
        endpoints.MapFinancingEndpoints();
        endpoints.MapInstrumentsEndpoints();
        endpoints.MapSubscriptionsEndpoints();
        endpoints.MapPartiesEndpoints();
        endpoints.MapReportingEndpoints();
        return endpoints;
    }
}
