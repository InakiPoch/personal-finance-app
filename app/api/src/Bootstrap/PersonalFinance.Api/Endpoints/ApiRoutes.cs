namespace PersonalFinance.Api.Endpoints;

public sealed record ApiRoutes {
    public sealed record Ledger {
        public const string Base = "/ledger";
        public const string Transactions = "/transactions";
        public const string Reversal = "/transactions/{id:guid}/reversal";
        public const string AccountBalance = "/accounts/{id:guid}/balance";
        public const string DevAccounts = "/accounts";
    }
}
