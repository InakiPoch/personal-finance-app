namespace PersonalFinance.Api.Endpoints;

public sealed record ApiRoutes {
    private const string V1 = "/v1";

    public sealed record Ledger {
        public const string Base = V1 + "/ledger";
        public const string Transactions = "/transactions";
        public const string Reversal = "/transactions/{id:guid}/reversal";
        public const string AccountBalance = "/accounts/{id:guid}/balance";
        public const string Expenses = "/expenses";
        public const string Incomes = "/incomes";
        public const string DevAccounts = "/accounts";
    }

    public sealed record Financing {
        public const string Base = V1 + "/financing";
        public const string PaymentPlans = "/payment-plans";
        public const string Statement = "/statements/{id:guid}";
        public const string StatementPayment = "/statements/{id:guid}/pay";
        public const string InstallmentPayment = "/installments/{id:guid}/pay";
        public const string FutureSchedule = "/cards/{id:guid}/future-schedule";
        public const string CardStatements = "/cards/{id:guid}/statements";
        public const string CardPurchases = "/cards/{id:guid}/purchases";
        public const string RecentPurchases = "/purchases/recent";
        public const string DueThisMonth = "/due-this-month";
        public const string CreditorPayables = "/creditor-payables";
        public const string CreditorPayableDetail = "/creditor-payables/{creditorId:guid}";
        public const string CreditorInstallmentPayment = "/creditor-installments/{id:guid}/pay";
        public const string CreditorInstallmentUnpayment = "/creditor-installments/{id:guid}/unpay";
        public const string CreditorInstallmentPartySharePayment = "/creditor-installments/{id:guid}/pay-party";
        public const string CreditorPayableFullPayment = "/creditor-payables/{creditorId:guid}/pay-full";
        public const string CreditorPurchasePayment = "/creditor-purchases/{paymentPlanId:guid}/pay";
    }

    public sealed record Instruments {
        public const string Base = V1 + "/instruments";
        public const string Create = "/";
        public const string List = "/";
        public const string CardClosingDay = "/cards/{id:guid}/closing-day";
        public const string CardClosingDates = "/cards/{id:guid}/closing-dates";
        public const string CardClosingDate = "/cards/{id:guid}/closing-dates/{year:int}/{month:int}";
    }

    public sealed record Creditors {
        public const string Base = V1 + "/creditors";
        public const string Create = "/";
        public const string List = "/";
    }

    public sealed record ExpenseCategories {
        public const string Base = V1 + "/expense-categories";
        public const string List = "/";
    }

    public sealed record Subscriptions {
        public const string Base = V1 + "/subscriptions";
        public const string Create = "/";
        public const string Cancel = "/{id:guid}";
        public const string Active = "/active";
        public const string ByMonth = "/by-month";
        public const string Pay = "/{id:guid}/pay";
        public const string Unpay = "/{id:guid}/unpay";
    }

    public sealed record Parties {
        public const string Base = V1 + "/parties";
        public const string Create = "/";
        public const string List = "/";
        public const string Settle = "/{id:guid}/settlements";
        public const string Loans = "/{id:guid}/loans";
        public const string Balance = "/{id:guid}/balance";
        public const string Timeline = "/{id:guid}/timeline";
        public const string FutureShares = "/{id:guid}/future-shares";
        public const string PendingShares = "/pending-shares";
    }

    public sealed record Reporting {
        public const string Base = V1 + "/reports";
        public const string MonthlyExpenses = "/monthly-expenses";
        public const string MonthlyIncomes = "/monthly-incomes";
        public const string MoneyFlow = "/money-flow";
        public const string Transactions = "/transactions";
        public const string TransactionById = "/transactions/{id:guid}";
        public const string CardDueByMonth = "/card-due-by-month";
        public const string PartyTimeline = "/parties/{id:guid}/timeline";
        public const string DebtSummary = "/parties/debt-summary";
        public const string OwedToYou = "/parties/owed-to-you";
    }
}
