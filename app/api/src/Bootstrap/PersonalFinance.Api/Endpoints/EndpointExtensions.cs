using PersonalFinance.Api.Endpoints.Financing;
using PersonalFinance.Api.Endpoints.Ledger;

namespace PersonalFinance.Api.Endpoints;

internal static class EndpointExtensions {
    extension(IEndpointRouteBuilder endpoints) {
        public IEndpointRouteBuilder MapLedgerEndpoints() {
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

        public IEndpointRouteBuilder MapFinancingEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Financing.Base);
            group.MapPost(ApiRoutes.Financing.PaymentPlans, PostPaymentPlan.Handle);
            group.MapPost(ApiRoutes.Financing.StatementPayment, PayStatement.Handle);
            group.MapGet(ApiRoutes.Financing.FutureSchedule, GetCardFutureSchedule.Handle);
            return endpoints;
        }
    }
}
