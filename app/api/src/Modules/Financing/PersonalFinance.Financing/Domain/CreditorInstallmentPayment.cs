using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

/// <summary>
/// One partial or full payment recorded against a creditor-financed <see cref="Installment"/>.
/// </summary>
internal sealed class CreditorInstallmentPayment : Entity<Guid> {
    public Guid InstallmentId { get; }
    public long AmountMinorUnits { get; }
    public DateTimeOffset PaidOnUtc { get; }
    public Guid? PartyId { get; }
    public Guid? SettlementTransactionId { get; }

    private CreditorInstallmentPayment(Guid id, Guid installmentId, long amountMinorUnits, DateTimeOffset paidOnUtc, Guid? partyId, Guid? settlementTransactionId) : base(id) {
        InstallmentId = installmentId;
        AmountMinorUnits = amountMinorUnits;
        PaidOnUtc = paidOnUtc;
        PartyId = partyId;
        SettlementTransactionId = settlementTransactionId;
    }

    internal static CreditorInstallmentPayment For(Guid installmentId, long amountMinorUnits, DateTimeOffset paidOnUtc, Guid? partyId = null, Guid? settlementTransactionId = null) {
        return new CreditorInstallmentPayment(Guid.CreateVersion7(), installmentId, amountMinorUnits, paidOnUtc, partyId, settlementTransactionId);
    }
}
