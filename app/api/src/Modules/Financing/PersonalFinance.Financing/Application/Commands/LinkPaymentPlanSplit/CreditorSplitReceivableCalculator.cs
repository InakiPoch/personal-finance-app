using PersonalFinance.Financing.Domain;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;

/// <summary>
/// Builds the Ledger legs that book the co-borrowers' shares of a card-less creditor-financed
/// split plan: <c>Dr Receivable_k</c> (each party's share) / <c>Cr CreditorPayable</c> (their sum).
/// </summary>
internal static class CreditorSplitReceivableCalculator {
    public static (IReadOnlyList<PostTransactionLine> Lines, long PartyPortionMinorUnits) BuildLines(
        Money amount,
        IReadOnlyList<PaymentPlanSplitParticipant> participants,
        Guid creditorPayableAccountId) {
        long[] weights = [1L, .. participants.Select(participant => participant.Weight)];
        var shares = new PhantomPennyAllocator().Allocate(amount, weights);
        var lines = new List<PostTransactionLine>();
        var partyPortion = 0L;
        for(var index = 0; index < participants.Count; index++) {
            var partyShare = shares[index + 1];
            if(partyShare.MinorUnits <= 0) {
                continue;
            }
            lines.Add(new PostTransactionLine(participants[index].ReceivableAccountId, DebitOrCredit.Debit, partyShare));
            partyPortion += partyShare.MinorUnits;
        }
        if(partyPortion == 0) {
            return ([], 0L);
        }
        lines.Add(new PostTransactionLine(creditorPayableAccountId, DebitOrCredit.Credit, Money.FromMinorUnits(partyPortion, amount.Currency)));
        return (lines, partyPortion);
    }

    /// <summary>
    /// The same per-party shares <see cref="BuildLines"/> books, without building Ledger legs
    /// </summary>
    public static IReadOnlyList<(Guid PartyId, long ShareMinorUnits)> PartyShares(
        Money amount,
        IReadOnlyList<PaymentPlanSplitParticipant> participantsOrderedByPartyId) {
        long[] weights = [1L, .. participantsOrderedByPartyId.Select(participant => participant.Weight)];
        var shares = new PhantomPennyAllocator().Allocate(amount, weights);
        return participantsOrderedByPartyId
            .Select((participant, index) => (participant.PartyId, shares[index + 1].MinorUnits))
            .ToList();
    }
}
