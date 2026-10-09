using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal sealed class Party : AggregateRoot<Guid> {
    public string Name { get; }
    public Guid ReceivableAccountId { get; }
    public Guid PayableAccountId { get; }

    private Party(Guid id, string name, Guid receivableAccountId, Guid payableAccountId) : base(id) {
        Name = name;
        ReceivableAccountId = receivableAccountId;
        PayableAccountId = payableAccountId;
    }

    public static Result<Party> Create(string name, Guid receivableAccountId, Guid payableAccountId) {
        if(string.IsNullOrWhiteSpace(name)) {
            return PartiesErrors.InvalidName;
        }
        return new Party(Guid.CreateVersion7(), name.Trim(), receivableAccountId, payableAccountId);
    }

    internal static Party Placeholder(Guid id, Guid receivableAccountId, Guid payableAccountId) {
        return new Party(id, $"Party {id}", receivableAccountId, payableAccountId);
    }
}
