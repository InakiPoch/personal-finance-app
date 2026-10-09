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
            group.MapPost(ApiRoutes.Ledger.Incomes, RecordIncome.Handle)
                .WithSummary("Record an income.")
                .WithDescription("Posts one balanced Ledger transaction for real money arriving in a Bank or Cash account, against the single lazily-created Income account. Never accepts a future date.")
                .Produces<RecordIncomeResultDto>(StatusCodes.Status201Created)
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
            group.MapGet(ApiRoutes.Financing.DueThisMonth, GetDueThisMonth.Handle)
                .WithSummary("Get what is due this month across cards and creditors.")
                .WithDescription("Optional month query parameter (yyyy-MM, default current month). Read-only dashboard roll-up, one row per (source, currency): unpaid card installments due by the current month (overdue included) plus creditor remaining amounts due now (same cutoff rule as creditor payables). Zero amounts are omitted.")
                .Produces<DueThisMonthDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest);
            group.MapGet(ApiRoutes.Financing.CreditorPayables, GetCreditorPayables.Handle)
                .WithSummary("List outstanding balances owed to creditors, grouped by creditor.")
                .WithDescription("Read-only roll-up over creditor-financed payment plans, in remaining (unpaid) amounts: \"due now\" folds in arrears up to the current billing cycle, \"total owed\" adds future installments. Fully or partially paid amounts are excluded. Card-backed plans never appear.")
                .Produces<CreditorPayablesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Financing.CreditorPayableDetail, GetCreditorDetail.Handle)
                .WithSummary("Get one creditor's outstanding debt, grouped by purchase.")
                .WithDescription("Read-only drill-down: every creditor-financed purchase (payment plan) for the given creditor with its installments listed beneath — sequence, remaining/paid amounts, due month, and paid/reversed status. An unknown creditor yields a 404.")
                .Produces<CreditorDetailDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapPost(ApiRoutes.Financing.CreditorInstallmentPayment, PayCreditorInstallment.Handle)
                .WithSummary("Record a payment on a creditor installment.")
                .WithDescription("Records a display-only payment (full or partial) on one creditor-financed installment — no bank account and no ledger posting (creditor debt is ledger-free for the holder). A null amount pays whatever remains. Fails if the installment is unknown, belongs to a credit-card plan, reversed, already fully paid, the amount is not positive, or it exceeds what remains.")
                .Produces<PayCreditorInstallmentResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapPost(ApiRoutes.Financing.CreditorInstallmentUnpayment, UnpayCreditorInstallment.Handle)
                .WithSummary("Undo the last payment on a creditor installment.")
                .WithDescription("Removes the most recent payment recorded on one creditor-financed installment (fat-finger recovery) — there is no ledger transaction to reverse. Fails if the installment is unknown, belongs to a credit-card plan, is reversed, or has no recorded payment to undo.")
                .Produces<PayCreditorInstallmentResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapPost(ApiRoutes.Financing.CreditorInstallmentPartySharePayment, PayCreditorInstallmentPartyShare.Handle)
                .WithSummary("Pay a split participant's share of a creditor installment.")
                .WithDescription("\"The party paid me, I pay the creditor\": settles the party's receivable into the given bank account (the same settlement recorded by hand on the Parties page) and records that share as a payment on the installment, in one call. Offered only once the installment's split receivable has been accrued. Fails if the installment is unknown, belongs to a credit-card plan, is reversed, its split hasn't accrued yet, the party isn't a participant, the party already paid this share, the share exceeds what remains, or the settlement itself fails (e.g. the party has no outstanding balance, or the bank account is unknown).")
                .Produces<PayCreditorInstallmentPartyShareResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Financing.CreditorPayableFullPayment, PayCreditorFullDebt.Handle)
                .WithSummary("Settle a creditor's entire remaining debt.")
                .WithDescription("Stamps a display-only PaidOnUtc on every unpaid, non-reversed installment across all of the creditor's purchases — no bank account and no ledger posting. Already-paid and reversed installments are skipped; the result carries the number newly settled (zero when the debt was already clear). An unknown creditor yields a 404.")
                .Produces<PayCreditorFullDebtResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapPost(ApiRoutes.Financing.CreditorPurchasePayment, PayCreditorExpense.Handle)
                .WithSummary("Pay one creditor-financed purchase.")
                .WithDescription("Records a display-only payment (full or partial) against one creditor-financed purchase — no bank account and no ledger posting. Fills that purchase's installments in sequence order, each taken in full until the amount runs out, the last one getting the leftover as a partial. A null amount pays whatever remains. Fails if the purchase is unknown, is card-backed, the amount is not positive, or it exceeds what remains.")
                .Produces<PayCreditorExpenseResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
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
            group.MapPut(ApiRoutes.Instruments.CardClosingDay, PutCardClosingDay.Handle)
                .WithSummary("Change a card's usual closing day.")
                .WithDescription("Sets the day (1-31) the card closes on in every month without a specific date, then re-buckets purchases whose billing month changes. Refused with 409 if a purchase that would move is already on a card statement.")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapGet(ApiRoutes.Instruments.CardClosingDates, GetCardClosingDates.Handle)
                .WithSummary("List a card's upcoming closing dates.")
                .WithDescription("Closing dates from the card's current open month forward (a month is locked once anything in it was charged to a statement), flagged when a month-specific date overrides the usual day.")
                .Produces<CardClosingDatesDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapPut(ApiRoutes.Instruments.CardClosingDate, PutCardClosingDate.Handle)
                .WithSummary("Set a card's closing day for one month.")
                .WithDescription("Overrides the closing day of one billing month that has not been charged yet, then re-buckets affected purchases. 409 if the month is locked or a charged purchase would move; 422 if the day is outside the month.")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapDelete(ApiRoutes.Instruments.CardClosingDate, DeleteCardClosingDate.Handle)
                .WithSummary("Reset a card's closing day for one month.")
                .WithDescription("Removes the month-specific closing day (back to the usual day), then re-buckets affected purchases. 409 if the month is locked or a charged purchase would move.")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
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
            group.MapGet(ApiRoutes.Subscriptions.ByMonth, GetSubscriptionsByMonth.Handle)
                .WithSummary("List the subscriptions renewing in a given month.")
                .WithDescription("Query parameter month (yyyy-MM, required). Returns every active subscription with its occurrence date in that month and a status of paid, overdue or upcoming. Past months derive the status from non-reversed ledger charges; templates store no start or cancellation date, so active subscriptions appear in every month.")
                .Produces<SubscriptionsByMonthDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest);
            group.MapPost(ApiRoutes.Subscriptions.Pay, PaySubscription.Handle)
                .WithSummary("Pay the next unpaid period.")
                .WithDescription("Posts one period's charge dated today, marks it paid, and advances the due date by one month. A subscription several months behind stays overdue until paid again.")
                .Produces<PaySubscriptionResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapPost(ApiRoutes.Subscriptions.Unpay, UnpaySubscription.Handle)
                .WithSummary("Undo the current period's payment.")
                .WithDescription("Reverses the pay-time Ledger transaction and steps the due date back one month. Fails if the current period was never paid.")
                .Produces<PaySubscriptionResultDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
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
            group.MapPost(ApiRoutes.Parties.Settle, PostSettlement.Handle)
                .WithSummary("Settle a party's current account.")
                .WithDescription("Posts a Ledger settlement against the party's receivable balance. Fails if the settlement would exceed what the party owes.")
                .Produces<SettlementResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Parties.Loans, PostLoan.Handle)
                .WithSummary("Record a loan to a party.")
                .WithDescription("Posts Dr party receivable / Cr the Bank or Cash source account, dated the given 'Lent on' day. Fails if the party is unknown (404), the amount is not positive, the currency is not ARS/USD, the source is not a Bank/Cash account, the description is blank, multi-line or over 120 characters, or the date is in the future (422).")
                .Produces<LoanResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Parties.Borrowings, PostBorrowing.Handle)
                .WithSummary("Record money borrowed from a party.")
                .WithDescription("Posts Dr the Bank or Cash destination account / Cr the party payable account (what you owe, never netted against what the party owes you), dated the given Borrowed on day. Fails if the party is unknown (404), the amount is not positive, the currency is not ARS/USD, the destination is not a Bank/Cash account, the description is blank, multi-line or over 120 characters, or the date is in the future (422). Undo with the generic ledger reversal.")
                .Produces<BorrowingResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Parties.Purchases, PostPartyPurchase.Handle)
                .WithSummary("Record a purchase a party paid.")
                .WithDescription("Kind 'debit' posts Dr the expense category / Cr the party payable account on the purchase date and returns the purchase id; no Bank or Cash account moves. The category is created if missing (case-insensitive). Fails if the party is unknown (404), the share is not positive, the currency is not ARS/USD, the description is blank, multi-line or over 120 characters, the category is blank, the kind is not supported, or the date is in the future (422).")
                .Produces<PartyPurchaseResultDto>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
            group.MapPost(ApiRoutes.Parties.UndoPurchase, PostUndoPartyPurchase.Handle)
                .WithSummary("Undo a party purchase as a whole.")
                .WithDescription("Reverses every ledger transaction the purchase posted. Fails if the purchase is unknown for that party (404) or was already undone (409).")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            group.MapPost(ApiRoutes.Parties.Repayments, PostRepayment.Handle)
                .WithSummary("Record paying back a party.")
                .WithDescription("Posts Dr the party payable account / Cr the Bank or Cash source account, dated the given Paid on day. Fails if the party is unknown (404), the amount exceeds what you currently owe in that currency (409), the amount is not positive, the currency is not ARS/USD, the source is not a Bank/Cash account, or the date is in the future (422). Undo with the generic ledger reversal.")
                .Produces<RepaymentResultDto>(StatusCodes.Status201Created)
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
            group.MapGet(ApiRoutes.Reporting.MonthlyIncomes, GetMonthlyIncomes.Handle)
                .WithSummary("Get monthly incomes.")
                .WithDescription("Cross-module dashboard over the Ledger monthly-incomes view, optionally filtered to one month.")
                .Produces<MonthlyIncomesDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.MoneyFlow, GetMoneyFlow.Handle)
                .WithSummary("Get the Money Flow table for one month.")
                .WithDescription("Accounting-style monthly rows over the Ledger money-flow view — an income credit or an out-of-pocket debit (my share only), reversed pairs hidden. The month query parameter is required.")
                .Produces<MoneyFlowDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest);
            group.MapGet(ApiRoutes.Reporting.Transactions, GetTransactionFeed.Handle)
                .WithSummary("List posted transactions with plain-language descriptions, newest first.")
                .WithDescription("Each row carries a type badge, a real description, from and to accounts and the bullet points describing what undoing it would change. Optionally narrowed to one account (?accountId=) and a posted-date range (?from=&to=, inclusive, yyyy-MM-dd).")
                .Produces<TransactionFeedDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest);
            group.MapGet(ApiRoutes.Reporting.TransactionById, GetTransactionFeedRow.Handle)
                .WithSummary("Get one transaction explained.")
                .WithDescription("Same row shape as the feed. 404 when the transaction does not exist.")
                .Produces<TransactionFeedRowDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
            group.MapGet(ApiRoutes.Reporting.CardDueByMonth, GetCardDueByMonth.Handle)
                .WithSummary("Get card liability due by month.")
                .WithDescription("Combines already-accrued card liability with the not-yet-accrued future installment schedule.")
                .Produces<CardDueByMonthDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.PartyTimeline, GetPartyTimeline.Handle)
                .WithSummary("Get a party's timeline (reporting view).")
                .WithDescription("Read-only dashboard equivalent of GET /v1/parties/{id}/timeline. The optional side query is receivable (default, what the party owes you) or payable (what you owe the party).")
                .Produces<PartyTimelineDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.DebtSummary, GetDebtSummary.Handle)
                .WithSummary("Get outstanding debt by party.")
                .WithDescription("Nets every party's Ledger movements to one outstanding-balance row per party.")
                .Produces<DebtByPartyDto>(StatusCodes.Status200OK);
            group.MapGet(ApiRoutes.Reporting.OwedToYou, GetOwedToYou.Handle)
                .WithSummary("Get what each party owes you as of a month.")
                .WithDescription("Receivable balances dated on or before the month end plus Scheduled shares due by that month, per party and currency; only positive amounts.")
                .Produces<OwedToYouDto>(StatusCodes.Status200OK);
            return endpoints;
        }
    }
}
