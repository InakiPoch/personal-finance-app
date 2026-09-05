using PersonalFinance.Financing.Domain;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Scheduling;

/// <summary>
/// Builds the Ledger legs for one installment of a card-less creditor-financed split plan.
/// </summary>
internal static class CreditorSplitAccrualCalculator {
    public static (IReadOnlyList<PostTransactionLine> Lines, long PartyPortionMinorUnits) BuildLines(
        Money installmentAmount,
        IReadOnlyList<PaymentPlanSplitParticipant> participants,
        Guid creditorPayableAccountId) {
        long[] weights = [1L, .. participants.Select(participant => participant.Weight)];
        var shares = new PhantomPennyAllocator().Allocate(installmentAmount, weights);
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
        lines.Add(new PostTransactionLine(creditorPayableAccountId, DebitOrCredit.Credit, Money.FromMinorUnits(partyPortion, installmentAmount.Currency)));
        return (lines, partyPortion);
    }
}
