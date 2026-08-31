using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal sealed class Party : AggregateRoot<Guid> {
    public string Name { get; }
    public Guid ReceivableAccountId { get; }

    private Party(Guid id, string name, Guid receivableAccountId) : base(id) {
        Name = name;
        ReceivableAccountId = receivableAccountId;
    }

    public static Result<Party> Create(string name, Guid receivableAccountId) {
        if(string.IsNullOrWhiteSpace(name)) {
            return PartiesErrors.InvalidName;
        }
        return new Party(Guid.CreateVersion7(), name.Trim(), receivableAccountId);
    }
}
