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
}
