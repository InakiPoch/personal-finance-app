using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class PaymentPlanSplitParticipant : Entity<Guid> {
    public Guid PaymentPlanId { get; }
    public Guid PartyId { get; }
    public Guid ReceivableAccountId { get; private set; }
    public long Weight { get; }

    private PaymentPlanSplitParticipant(Guid id, Guid paymentPlanId, Guid partyId, long weight) : base(id) {
        PaymentPlanId = paymentPlanId;
        PartyId = partyId;
        Weight = weight;
    }

    internal static PaymentPlanSplitParticipant For(Guid paymentPlanId, Guid partyId, long weight) {
        return new PaymentPlanSplitParticipant(Guid.CreateVersion7(), paymentPlanId, partyId, weight);
    }

    internal void AssignReceivableAccount(Guid receivableAccountId) {
        ReceivableAccountId = receivableAccountId;
    }
}
