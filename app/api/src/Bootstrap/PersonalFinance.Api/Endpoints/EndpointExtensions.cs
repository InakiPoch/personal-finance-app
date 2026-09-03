using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Financing;
using PersonalFinance.Api.Endpoints.Ledger;
using PersonalFinance.Api.Endpoints.Parties;
using PersonalFinance.Api.Endpoints.Reporting;
using PersonalFinance.Api.Endpoints.Subscriptions;

namespace PersonalFinance.Api.Endpoints;

internal static class EndpointExtensions {
    extension(IEndpointRouteBuilder endpoints) {
        public IEndpointRouteBuilder MapLedgerEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Ledger.Base)
                .WithTags("Ledger")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapPost(ApiRoutes.Ledger.Transactions, PostTransaction.Handle)
                .WithSummary("Post a balanced double-entry transaction.")
                .WithDescription("Creates an append-only transaction with two or more entries whose debits equal its credits. Corrections are never edits — see POST .../reversal.")
                .Produces<PostTransactionResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Ledger.Reversal, ReverseTransaction.Handle)
                .WithSummary("Reverse a posted transaction.")
                .WithDescription("Posts a storno reversal of the given transaction, plus a compensating card-credit entry if the reversed installment was already paid. Always succeeds unless the transaction is missing or is itself a reversal.")
                .Produces<ReverseTransactionResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapGet(ApiRoutes.Ledger.AccountBalance, GetAccountBalance.Handle)
                .WithSummary("Get an account's current balance.")
                .WithDescription("Returns the live balance of the given Ledger account.")
                .Produces<AccountBalanceDto>(StatusCodes.Status200OK);
            var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
            if(environment.IsDevelopment()) {
                group.MapPost(ApiRoutes.Ledger.DevAccounts, PostDevAccount.Handle)
                    .WithSummary("Create a Ledger account (development only).")
                    .WithDescription("Development-only shortcut for provisioning a Ledger account directly. Superseded by POST /instruments; not present outside Development.")
                    .Produces<CreateAccountResultDto>(StatusCodes.Status200OK)
                    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            }
            return endpoints;
        }

        public IEndpointRouteBuilder MapFinancingEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Financing.Base)
                .WithTags("Financing")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapPost(ApiRoutes.Financing.PaymentPlans, PostPaymentPlan.Handle)
                .WithSummary("Open a payment plan on a credit card.")
                .WithDescription("Creates an installment plan against an existing credit card, optionally split across parties.")
                .Produces<CreatePaymentPlanResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapGet(ApiRoutes.Financing.Statement, GetStatement.Handle)
                .WithSummary("Get a monthly statement and its component installments.")
                .WithDescription("Returns the accrued installments that make up a card statement's amount due, for review before payment.")
                .Produces<MonthlyStatementDetailDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapPost(ApiRoutes.Financing.StatementPayment, PayStatement.Handle)
                .WithSummary("Pay a monthly statement.")
                .WithDescription("Posts a Ledger payment for the given statement, netting any carried card credit. Fails if the statement is unknown or already paid.")
                .Produces<PayStatementResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapGet(ApiRoutes.Financing.FutureSchedule, GetCardFutureSchedule.Handle)
                .WithSummary("Get a card's future installment schedule.")
                .WithDescription("Returns the not-yet-accrued installments for the given credit card.")
                .Produces<CardFutureScheduleDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Financing.CardStatements, GetCardStatements.Handle)
                .WithSummary("List a card's monthly statements.")
                .WithDescription("Returns every monthly statement raised for the given credit card, ordered by billing cycle, without the component installments. An unknown card yields an empty list.")
                .Produces<CardStatementsDto>(StatusCodes.Status200OK);
            return endpoints;
        }

        public IEndpointRouteBuilder MapInstrumentsEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Instruments.Base)
                .WithTags("Instruments")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapPost(ApiRoutes.Instruments.Create, PostInstrument.Handle)
                .WithSummary("Register a payment instrument.")
                .WithDescription("Unified instrument registration: routes to Ledger for debit/cash accounts or Financing for credit cards, by request type.")
                .Produces<InstrumentCreatedDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapGet(ApiRoutes.Instruments.List, GetInstruments.Handle)
                .WithSummary("List registered payment instruments.")
                .WithDescription("Unified read of every registered instrument: debit and cash accounts from Ledger plus credit cards from Financing, each tagged with its instrument type.")
                .Produces<InstrumentsListDto>(StatusCodes.Status200OK);
            return endpoints;
        }

        public IEndpointRouteBuilder MapSubscriptionsEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Subscriptions.Base)
                .WithTags("Subscriptions")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapPost(ApiRoutes.Subscriptions.Create, PostSubscription.Handle)
                .WithSummary("Create a subscription and charge its first period.")
                .WithDescription("Provisions a dedicated expense account and posts the first period's charge synchronously; every later period is charged by the renewal scheduler.")
                .Produces<SubscriptionResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapDelete(ApiRoutes.Subscriptions.Cancel, DeleteSubscription.Handle)
                .WithSummary("Cancel a subscription.")
                .WithDescription("Deactivates the subscription so it stops renewing. Past charges are untouched — this is never a delete of history.")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapGet(ApiRoutes.Subscriptions.Active, GetActiveSubscriptions.Handle)
                .WithSummary("List active subscriptions.")
                .WithDescription("Returns every subscription that is currently active.")
                .Produces<ActiveSubscriptionsDto>(StatusCodes.Status200OK);
            return endpoints;
        }

        public IEndpointRouteBuilder MapPartiesEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Parties.Base)
                .WithTags("Parties")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapPost(ApiRoutes.Parties.Create, PostParty.Handle)
                .WithSummary("Create a party.")
                .WithDescription("Registers a third party for shared-expense tracking.")
                .Produces<PartyResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Parties.SharedExpenses, PostSharedExpense.Handle)
                .WithSummary("Register a shared expense.")
                .WithDescription("Posts one multi-leg Ledger transaction splitting an expense across the caller and one or more parties.")
                .Produces<SharedExpenseResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Parties.Settle, PostSettlement.Handle)
                .WithSummary("Settle a party's current account.")
                .WithDescription("Posts a Ledger settlement against the party's receivable balance. Fails if the settlement would exceed what the party owes.")
                .Produces<SettlementResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapGet(ApiRoutes.Parties.Balance, GetBalance.Handle)
                .WithSummary("Get a party's current account balance.")
                .WithDescription("Returns the live balance of the party's Ledger receivable account.")
                .Produces<CurrentAccountBalanceDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Parties.Timeline, GetTimeline.Handle)
                .WithSummary("Get a party's current account timeline.")
                .WithDescription("Returns the chronological movements on the party's Ledger receivable account.")
                .Produces<CurrentAccountTimelineDto>(StatusCodes.Status200OK);
            return endpoints;
        }

        public IEndpointRouteBuilder MapReportingEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Reporting.Base)
                .WithTags("Reporting")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapGet(ApiRoutes.Reporting.MonthlyExpenses, GetMonthlyExpenses.Handle)
                .WithSummary("Get monthly debit/cash expenses.")
                .WithDescription("Cross-module dashboard over the Ledger monthly-expenses view, optionally filtered to one month.")
                .Produces<MonthlyExpensesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.CardDueByMonth, GetCardDueByMonth.Handle)
                .WithSummary("Get card liability due by month.")
                .WithDescription("Combines already-accrued card liability with the not-yet-accrued future installment schedule.")
                .Produces<CardDueByMonthDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.PartyTimeline, GetPartyTimeline.Handle)
                .WithSummary("Get a party's timeline (reporting view).")
                .WithDescription("Read-only dashboard equivalent of GET /v1/parties/{id}/timeline.")
                .Produces<PartyTimelineDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.DebtSummary, GetDebtSummary.Handle)
                .WithSummary("Get outstanding debt by party.")
                .WithDescription("Nets every party's Ledger movements to one outstanding-balance row per party.")
                .Produces<DebtByPartyDto>(StatusCodes.Status200OK);
            return endpoints;
        }
    }
}
