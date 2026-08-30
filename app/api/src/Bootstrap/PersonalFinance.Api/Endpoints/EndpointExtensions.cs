using PersonalFinance.Api.Endpoints.Ledger;

namespace PersonalFinance.Api.Endpoints;

internal static class EndpointExtensions {
    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(ApiRoutes.Ledger.Base);
        group.MapPost(ApiRoutes.Ledger.Transactions, PostTransaction.Handle);
        group.MapPost(ApiRoutes.Ledger.Reversal, ReverseTransaction.Handle);
        group.MapGet(ApiRoutes.Ledger.AccountBalance, GetAccountBalance.Handle);
        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if(environment.IsDevelopment()) {
            group.MapPost(ApiRoutes.Ledger.DevAccounts, PostDevAccount.Handle);
        }
        return endpoints;
    }
}
