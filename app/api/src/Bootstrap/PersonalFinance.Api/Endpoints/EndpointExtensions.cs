using PersonalFinance.Api.Endpoints.Creditors;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.ExpenseCategories;
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
            group.MapGet(ApiRoutes.Ledger.Transactions, GetTransactions.Handle)
                .WithSummary("List posted transactions, newest first.")
                .WithDescription("Returns the Ledger transaction feed with a synthesized label per row, optionally narrowed to one account (?accountId=) and a posted-date range (?from=&to=, inclusive, yyyy-MM-dd). Each row flags whether it is itself a reversal and whether it has since been reversed.")
                .Produces<TransactionFeedDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest);
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
            group.MapPost(ApiRoutes.Ledger.Expenses, RecordDebitExpense.Handle)
                .WithSummary("Record a debit or cash expense.")
                .WithDescription("Posts one balanced Ledger transaction for money already spent from a debit or cash account, against an expense category resolved get-or-create by name. When parties are split in, each share posts to their receivable instead — the holder's share stays on the category — mirroring the credit-card split.")
                .Produces<RecordDebitExpenseResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
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
            group.MapPost(ApiRoutes.Financing.InstallmentPayment, PayInstallment.Handle)
                .WithSummary("Pay a single installment.")
                .WithDescription("Posts a plain Dr CardLiability / Cr Bank for one accrued installment and marks it paid, without touching carried card credit. The parent statement is marked paid only once all its accrued, non-reversed installments are paid. Fails if the installment is unknown, reversed, already paid, or not yet accrued.")
                .Produces<PayInstallmentResultDto>(StatusCodes.Status201Created)
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
            group.MapGet(ApiRoutes.Financing.CardPurchases, GetCardPurchases.Handle)
                .WithSummary("List a card's outstanding purchases.")
                .WithDescription("Returns every outstanding purchase (payment plan) on the given credit card, current-cycle purchases first. An unknown card yields an empty list.")
                .Produces<CardPurchasesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Financing.RecentPurchases, GetRecentPurchases.Handle)
                .WithSummary("List recent purchases across every card.")
                .WithDescription("Returns every payment plan, newest-first, independent of card grouping or debt state, capped at a default limit.")
                .Produces<RecentPurchasesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Financing.CreditorPayables, GetCreditorPayables.Handle)
                .WithSummary("List outstanding balances owed to creditors, grouped by creditor.")
                .WithDescription("Read-only roll-up over creditor-financed payment plans. There is no per-installment paid/settled flag yet, so \"outstanding\" is the whole plan: every non-reversed installment of a creditor-financed plan counts as still owed. Card-backed plans never appear.")
                .Produces<CreditorPayablesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Financing.CreditorPayableDetail, GetCreditorDetail.Handle)
                .WithSummary("Get one creditor's outstanding debt, grouped by purchase.")
                .WithDescription("Read-only drill-down: every creditor-financed purchase (payment plan) for the given creditor with its installments listed beneath — sequence, amount, due month, and paid/reversed status. An unknown creditor yields a 404.")
                .Produces<CreditorDetailDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapPost(ApiRoutes.Financing.CreditorInstallmentPayment, PayCreditorInstallment.Handle)
                .WithSummary("Mark a creditor installment paid.")
                .WithDescription("Stamps a display-only PaidOnUtc on one creditor-financed installment — no bank account and no ledger posting (creditor debt is ledger-free for the holder). Fails if the installment is unknown, belongs to a credit-card plan, reversed, or already paid.")
                .Produces<PayCreditorInstallmentResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapPost(ApiRoutes.Financing.CreditorInstallmentUnpayment, UnpayCreditorInstallment.Handle)
                .WithSummary("Undo a creditor installment payment.")
                .WithDescription("Clears the display-only PaidOnUtc stamp on one creditor-financed installment (fat-finger recovery). There is no ledger transaction to reverse. Fails if the installment is unknown, belongs to a credit-card plan, or is reversed.")
                .Produces<PayCreditorInstallmentResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
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

        public IEndpointRouteBuilder MapCreditorEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.Creditors.Base)
                .WithTags("Creditors")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapPost(ApiRoutes.Creditors.Create, PostCreditor.Handle)
                .WithSummary("Register a creditor together with its accounts.")
                .WithDescription("Creates a creditor and its destination accounts in one submit. Accounts are optional and free-text (label + CBU/CVU/alias identifier).")
                .Produces<CreditorResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapGet(ApiRoutes.Creditors.List, GetCreditors.Handle)
                .WithSummary("List registered creditors.")
                .WithDescription("Returns every creditor with its accounts, ordered by name.")
                .Produces<CreditorListDto>(StatusCodes.Status200OK);
            return endpoints;
        }

        public IEndpointRouteBuilder MapExpenseCategoriesEndpoints() {
            var group = endpoints.MapGroup(ApiRoutes.ExpenseCategories.Base)
                .WithTags("Expense categories")
                .ProducesProblem(StatusCodes.Status500InternalServerError);
            group.MapGet(ApiRoutes.ExpenseCategories.List, GetExpenseCategories.Handle)
                .WithSummary("List debit/cash expense categories.")
                .WithDescription("Returns the distinct expense-category names available for a debit or cash expense — Ledger accounts of Expense type and Expense kind — ordered by name. New categories are minted on first use when the expense is recorded.")
                .Produces<ExpenseCategoriesDto>(StatusCodes.Status200OK);
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
            group.MapGet(ApiRoutes.Parties.List, GetParties.Handle)
                .WithSummary("List registered parties.")
                .WithDescription("Returns every registered party, ordered by name.")
                .Produces<PartiesListDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Parties.Balance, GetBalance.Handle)
                .WithSummary("Get a party's current account balance.")
                .WithDescription("Returns the live balance of the party's Ledger receivable account.")
                .Produces<CurrentAccountBalanceDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Parties.Timeline, GetTimeline.Handle)
                .WithSummary("Get a party's current account timeline.")
                .WithDescription("Returns the chronological movements on the party's Ledger receivable account.")
                .Produces<CurrentAccountTimelineDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Parties.FutureShares, GetPartyFutureShares.Handle)
                .WithSummary("A party's upcoming installment shares.")
                .WithDescription("Projected not-yet-accrued monthly shares for the party's card-split plans.")
                .Produces<FuturePartySharesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Parties.PendingShares, GetPendingSharesByParty.Handle)
                .WithSummary("Pending scheduled installment shares per party.")
                .WithDescription("One row per party with not-yet-accrued card-split or creditor-financed split shares — count and summed minor units. Lets the Parties list tell a truly-settled party from one that owes $0 now but has installments scheduled.")
                .Produces<PendingSharesByPartyDto>(StatusCodes.Status200OK);
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
