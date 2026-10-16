using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

/// <summary>
/// Pays a creditor's whole debt, or a set amount of one currency, from a Bank/Cash account.
/// </summary>
internal sealed class PayCreditorFullDebtHandler(FinancingDbContext context, TimeProvider timeProvider, ILedgerApi ledgerApi) : ICommandHandler<PayCreditorFullDebtCommand, int> {
    public async Task<Result<int>> HandleAsync(PayCreditorFullDebtCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorFullDebtValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var creditorName = await CreditorPostingHelper.LoadCreditorNameAsync(context, command.CreditorId, cancellationToken);
        if(creditorName is null) {
            return FinancingErrors.CreditorNotFound;
        }
        var paidFrom = await CreditorPostingHelper.ValidatePaidFromAsync(ledgerApi, command.BankAccountId, cancellationToken);
        if(paidFrom.IsFailure) {
            return paidFrom.Error;
        }
        var installments = await (
            from installment in context.Set<Installment>().Include(candidate => candidate.Payments)
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId == command.CreditorId
            select new { installment, plan }
        ).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var pieces = new List<(Installment Installment, CreditorInstallmentPayment Payment)>();
        if(command.AmountMinorUnits is null) {
            var settledAll = 0;
            foreach(var row in installments) {
                if(row.installment.IsReversed || row.installment.RemainingMinorUnits == 0) {
                    continue;
                }
                var applied = row.installment.ApplyPayment(row.installment.RemainingMinorUnits, now);
                if(applied.IsFailure) {
                    return applied.Error;
                }
                pieces.Add((row.installment, applied.Value));
                settledAll++;
            }
            return await postAndSaveAsync(command, creditorName, installments.Select(row => row.plan).DistinctBy(plan => plan.Id), pieces, settledAll, now, cancellationToken);
        }
        var candidates = installments
            .Where(row => !row.installment.IsReversed && row.installment.RemainingMinorUnits > 0 && row.installment.Currency.Code == command.CurrencyCode)
            .OrderBy(row => row.installment.DueCycle.Year)
            .ThenBy(row => row.installment.DueCycle.Month)
            .ThenBy(row => row.plan.PurchaseDate)
            .ThenBy(row => row.installment.Sequence)
            .ToList();
        var totalRemaining = candidates.Sum(candidate => candidate.installment.RemainingMinorUnits);
        var amount = command.AmountMinorUnits.Value;
        if(amount <= 0) {
            return FinancingErrors.InvalidPaymentAmount;
        }
        if(amount > totalRemaining) {
            return FinancingErrors.PaymentExceedsRemaining;
        }
        var byId = candidates.ToDictionary(candidate => candidate.installment.Id);
        var allocation = CreditorPaymentWaterfall.Allocate(
            candidates.Select(candidate => (candidate.installment.Id, candidate.installment.RemainingMinorUnits)), amount);
        var settled = 0;
        foreach(var (installmentId, pieceAmount) in allocation) {
            var row = byId[installmentId];
            var installment = row.installment;
            var applied = installment.ApplyPayment(pieceAmount, now);
            if(applied.IsFailure) {
                return applied.Error;
            }
            pieces.Add((installment, applied.Value));
            if(installment.RemainingMinorUnits == 0) {
                settled++;
            }
        }
        return await postAndSaveAsync(command, creditorName, installments.Select(row => row.plan).DistinctBy(plan => plan.Id), pieces, settled, now, cancellationToken);
    }

    private async Task<Result<int>> postAndSaveAsync(PayCreditorFullDebtCommand command, string creditorName, IEnumerable<PaymentPlan> plans, List<(Installment Installment, CreditorInstallmentPayment Payment)> pieces, int settled, DateTimeOffset now, CancellationToken cancellationToken) {
        var splits = await CreditorPostingHelper.LoadSplitsAsync(context, plans, cancellationToken);
        var postedPieces = await CreditorPostingHelper.PostPiecesAsync(ledgerApi, command.BankAccountId, pieces, splits, CreditorPaymentDescriptionHelper.ForFullDebt(creditorName), now, cancellationToken);
        if(postedPieces.IsFailure) {
            return postedPieces.Error;
        }
        await CreditorPostingHelper.SaveOrReverseAsync(context, ledgerApi, postedPieces.Value, now, cancellationToken);
        return settled;
    }
}
