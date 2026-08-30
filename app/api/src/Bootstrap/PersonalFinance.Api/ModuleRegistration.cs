using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Api.Endpoints;
using PersonalFinance.Ledger;

namespace PersonalFinance.Api;

internal static class ModuleRegistration {
    private static readonly IModule[] modules = [new LedgerModule()];

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
        return endpoints;
    }
}
