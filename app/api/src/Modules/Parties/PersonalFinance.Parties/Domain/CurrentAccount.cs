using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

/// <summary>
/// A party's current-account position — a read-time projection of its Ledger receivable balance.
/// </summary>
internal sealed record CurrentAccount(Guid PartyId, string PartyName, Money Balance) {
    public static CurrentAccount Project(Party party, Money ledgerReceivableBalance) {
        return new CurrentAccount(party.Id, party.Name, ledgerReceivableBalance);
    }
}
