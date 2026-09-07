namespace PersonalFinance.Api.Endpoints;

public sealed record ApiRoutes {
    private const string V1 = "/v1";

    public sealed record Ledger {
        public const string Base = V1 + "/ledger";
        public const string Transactions = "/transactions";
        public const string Reversal = "/transactions/{id:guid}/reversal";
        public const string AccountBalance = "/accounts/{id:guid}/balance";
        public const string Expenses = "/expenses";
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
        public const string CreditorPayables = "/creditor-payables";
    }

    public sealed record Instruments {
        public const string Base = V1 + "/instruments";
        public const string Create = "/";
        public const string List = "/";
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
    }

    public sealed record Parties {
        public const string Base = V1 + "/parties";
        public const string Create = "/";
        public const string List = "/";
        public const string SharedExpenses = "/shared-expenses";
        public const string Settle = "/{id:guid}/settlements";
        public const string Balance = "/{id:guid}/balance";
        public const string Timeline = "/{id:guid}/timeline";
        public const string FutureShares = "/{id:guid}/future-shares";
        public const string PendingShares = "/pending-shares";
    }

    public sealed record Reporting {
        public const string Base = V1 + "/reports";
        public const string MonthlyExpenses = "/monthly-expenses";
        public const string CardDueByMonth = "/card-due-by-month";
        public const string PartyTimeline = "/parties/{id:guid}/timeline";
        public const string DebtSummary = "/parties/debt-summary";
    }
}
