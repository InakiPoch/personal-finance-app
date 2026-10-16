using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands;

/// <summary>
/// Posts and reverses the Bank/Cash outflow of creditor pay actions.
/// </summary>
internal static class CreditorPostingHelper {
    internal const string CreditorPaymentsCategory = "Creditor payments";

    public static async Task<Result> ValidatePaidFromAsync(ILedgerApi ledgerApi, Guid bankAccountId, CancellationToken cancellationToken) {
        var instruments = await ledgerApi.ListInstrumentAccountsAsync(new ListInstrumentAccountsQuery(), cancellationToken);
        return instruments.Rows.Any(row => row.AccountId == bankAccountId) ? Result.Success() : Result.Failure(FinancingErrors.InvalidPaidFromAccount);
    }

    /// <summary>
    /// The party-split context of a plan: the account fronted shares are debited to and the parties whose shares may still be unpaid.
    /// </summary>
    public sealed record PartySplit(Guid PayableAccountId, IReadOnlyList<PaymentPlanSplitParticipant> Participants);

    public static async Task<IReadOnlyDictionary<Guid, PartySplit>> LoadSplitsAsync(FinancingDbContext context, IEnumerable<PaymentPlan> plans, CancellationToken cancellationToken) {
        var splitPlans = plans.Where(plan => plan.SplitReferenceId is not null && plan.CreditorPayableAccountId is not null).ToDictionary(plan => plan.Id);
        var planIds = splitPlans.Keys.ToList();
        var participants = await context.Set<PaymentPlanSplitParticipant>()
            .Where(participant => planIds.Contains(participant.PaymentPlanId))
            .OrderBy(participant => participant.PartyId)
        .ToListAsync(cancellationToken);
        return splitPlans.ToDictionary(
            entry => entry.Key,
            entry => new PartySplit(entry.Value.CreditorPayableAccountId!.Value, participants.Where(participant => participant.PaymentPlanId == entry.Key).ToList())
        );
    }

    private static (long Own, long Fronted) divide(Installment installment, CreditorInstallmentPayment payment, PartySplit? split) {
        if(split is null) {
            return (payment.AmountMinorUnits, 0);
        }
        var paidParties = installment.Payments.Where(candidate => candidate.PartyId is not null).Select(candidate => candidate.PartyId!.Value).ToHashSet();
        var partiesUnpaid = CreditorSplitReceivableCalculator.PartyShares(installment.Amount, split.Participants)
            .Where(share => !paidParties.Contains(share.PartyId))
        .Sum(share => share.ShareMinorUnits);
        var ownRemaining = Math.Max(0, installment.RemainingMinorUnits + payment.AmountMinorUnits - partiesUnpaid);
        var own = Math.Min(payment.AmountMinorUnits, ownRemaining);
        return (own, payment.AmountMinorUnits - own);
    }

    /// <summary>
    /// Posts one transaction per currency for a waterfall action's pieces and stamps its id on each payment row.
    /// </summary>
    public static async Task<Result<IReadOnlyList<Guid>>> PostPiecesAsync(ILedgerApi ledgerApi, Guid bankAccountId, IReadOnlyList<(Installment Installment, CreditorInstallmentPayment Payment)> pieces, IReadOnlyDictionary<Guid, PartySplit> splits, string description, DateTimeOffset now, CancellationToken cancellationToken) {
        var posted = new List<Guid>();
        foreach(var group in pieces.GroupBy(piece => piece.Installment.Currency)) {
            var lines = new List<PostTransactionLine>();
            var divided = group.Select(piece => (piece, Parts: divide(piece.Installment, piece.Payment, splits.GetValueOrDefault(piece.Installment.PaymentPlanId)))).ToList();
            var own = divided.Sum(entry => entry.Parts.Own);
            if(own > 0) {
                var category = await ledgerApi.GetOrCreateExpenseCategoryAsync(new GetOrCreateExpenseCategoryCommand(CreditorPaymentsCategory), cancellationToken);
                if(category.IsFailure) {
                    await ReverseAsync(ledgerApi, posted, now);
                    return category.Error;
                }
                lines.Add(new PostTransactionLine(category.Value, DebitOrCredit.Debit, Money.FromMinorUnits(own, group.Key)));
            }
            foreach(var fronted in divided.Where(entry => entry.Parts.Fronted > 0).GroupBy(entry => splits[entry.piece.Installment.PaymentPlanId].PayableAccountId)) {
                lines.Add(new PostTransactionLine(fronted.Key, DebitOrCredit.Debit, Money.FromMinorUnits(fronted.Sum(entry => entry.Parts.Fronted), group.Key)));
            }
            lines.Add(new PostTransactionLine(bankAccountId, DebitOrCredit.Credit, Money.FromMinorUnits(group.Sum(piece => piece.Payment.AmountMinorUnits), group.Key)));
            var result = await ledgerApi.PostTransactionAsync(new PostTransactionCommand(lines, now, Description: description), cancellationToken);
            if(result.IsFailure) {
                await ReverseAsync(ledgerApi, posted, now);
                return result.Error;
            }
            posted.Add(result.Value);
            foreach(var piece in group) {
                piece.Payment.RecordLedgerTransaction(result.Value);
            }
        }
        return posted;
    }

    public static async Task<string?> LoadCreditorNameAsync(FinancingDbContext context, Guid? creditorId, CancellationToken cancellationToken) {
        return await context.Creditors
            .Where(candidate => candidate.Id == creditorId)
            .Select(candidate => candidate.Name)
        .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<Result<Guid>> PostPayableAsync(ILedgerApi ledgerApi, Guid bankAccountId, Guid payableAccountId, Money amount, string description, DateTimeOffset now, CancellationToken cancellationToken) {
        PostTransactionLine[] lines = [
            new(payableAccountId, DebitOrCredit.Debit, amount),
            new(bankAccountId, DebitOrCredit.Credit, amount)
        ];
        return await ledgerApi.PostTransactionAsync(new PostTransactionCommand(lines, now, Description: description), cancellationToken);
    }

    public static async Task SaveOrReverseAsync(FinancingDbContext context, ILedgerApi ledgerApi, IReadOnlyList<Guid> transactionIds, DateTimeOffset now, CancellationToken cancellationToken) {
        try {
            await context.SaveChangesAsync(cancellationToken);
        } catch {
            await ReverseAsync(ledgerApi, transactionIds, now);
            throw;
        }
    }

    public static async Task ReverseAsync(ILedgerApi ledgerApi, IEnumerable<Guid> transactionIds, DateTimeOffset now) {
        foreach(var transactionId in transactionIds) {
            await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(transactionId, now), CancellationToken.None);
        }
    }
}
