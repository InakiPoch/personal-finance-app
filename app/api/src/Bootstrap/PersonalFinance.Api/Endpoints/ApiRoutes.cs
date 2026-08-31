namespace PersonalFinance.Api.Endpoints;

public sealed record ApiRoutes {
    private const string V1 = "/v1";

    public sealed record Ledger {
        public const string Base = V1 + "/ledger";
        public const string Transactions = "/transactions";
        public const string Reversal = "/transactions/{id:guid}/reversal";
        public const string AccountBalance = "/accounts/{id:guid}/balance";
        public const string DevAccounts = "/accounts";
    }

    public sealed record Financing {
        public const string Base = V1 + "/financing";
        public const string PaymentPlans = "/payment-plans";
        public const string StatementPayment = "/statements/{id:guid}/pay";
        public const string FutureSchedule = "/cards/{id:guid}/future-schedule";
    }

    public sealed record Instruments {
        public const string Base = V1 + "/instruments";
        public const string Create = "/";
    }

    public sealed record Subscriptions {
        public const string Base = V1 + "/subscriptions";
        public const string Create = "/";
        public const string Cancel = "/{id:guid}";
        public const string Active = "/active";
    }
}
