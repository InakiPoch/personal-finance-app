using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class CreditorAccount : Entity<Guid> {
    public Guid CreditorId { get; }
    public string Label { get; }
    public string? Identifier { get; private set; }

    private CreditorAccount(Guid id, Guid creditorId, string label) : base(id) {
        CreditorId = creditorId;
        Label = label;
    }

    internal static CreditorAccount Create(Guid creditorId, string label, string? identifier) {
        var account = new CreditorAccount(Guid.CreateVersion7(), creditorId, label) {
            Identifier = string.IsNullOrWhiteSpace(identifier) ? null : identifier.Trim()
        };
        return account;
    }
}
