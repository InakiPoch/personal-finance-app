using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class Creditor : AggregateRoot<Guid> {
    public string Name { get; }
    public IReadOnlyList<CreditorAccount> Accounts => accounts;

    private readonly List<CreditorAccount> accounts = [];

    private Creditor(Guid id, string name) : base(id) {
        Name = name;
    }

    public static Result<Creditor> Create(Guid id, string name, IReadOnlyList<(string Label, string? Identifier)> accounts) {
        if(string.IsNullOrWhiteSpace(name)) {
            return FinancingErrors.InvalidCreditorName;
        }
        var creditor = new Creditor(id, name.Trim());
        foreach(var account in accounts) {
            creditor.accounts.Add(CreditorAccount.Create(creditor.Id, account.Label, account.Identifier));
        }
        return creditor;
    }
}
