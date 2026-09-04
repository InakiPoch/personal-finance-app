using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class CreditorAccount : Entity<Guid> {
    public Guid CreditorId { get; }
    public string Label { get; }
    public string Identifier { get; }

    private CreditorAccount(Guid id, Guid creditorId, string label, string identifier) : base(id) {
        CreditorId = creditorId;
        Label = label;
        Identifier = identifier;
    }

    internal static CreditorAccount Create(Guid creditorId, string label, string identifier) {
        return new CreditorAccount(Guid.CreateVersion7(), creditorId, label, identifier);
    }
}
