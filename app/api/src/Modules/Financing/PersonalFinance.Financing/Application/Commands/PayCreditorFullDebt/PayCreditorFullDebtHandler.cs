using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

/// <summary>
/// Settles a creditor's debt in one transaction. A null amount pays every unpaid, non-reversed
/// installment across all of that creditor's purchases in full. A set amount fills only that currency's remaining installments
/// </summary>
internal sealed class PayCreditorFullDebtHandler(FinancingDbContext context, TimeProvider timeProvider, ILedgerApi ledgerApi) : ICommandHandler<PayCreditorFullDebtCommand, int> {
    public async Task<Result<int>> HandleAsync(PayCreditorFullDebtCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorFullDebtValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var creditorName = await context.Creditors
            .Where(candidate => candidate.Id == command.CreditorId)
            .Select(candidate => candidate.Name)
            .FirstOrDefaultAsync(cancellationToken);
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
            select new { installment, plan.PurchaseDate, plan.SplitReferenceId }
        ).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var pieces = new List<(Installment Installment, CreditorInstallmentPayment Payment, bool MovesBank)>();
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
                pieces.Add((row.installment, applied.Value, row.SplitReferenceId is null));
                settledAll++;
            }
            return await SaveAsync(command, creditorName, pieces, settledAll, now, cancellationToken);
        }
        var candidates = installments
            .Where(row => !row.installment.IsReversed && row.installment.RemainingMinorUnits > 0 && row.installment.Currency.Code == command.CurrencyCode)
            .OrderBy(row => row.installment.DueCycle.Year)
            .ThenBy(row => row.installment.DueCycle.Month)
            .ThenBy(row => row.PurchaseDate)
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
            pieces.Add((installment, applied.Value, row.SplitReferenceId is null));
            if(installment.RemainingMinorUnits == 0) {
                settled++;
            }
        }
        return await SaveAsync(command, creditorName, pieces, settled, now, cancellationToken);
    }

    private async Task<Result<int>> SaveAsync(PayCreditorFullDebtCommand command, string creditorName, List<(Installment Installment, CreditorInstallmentPayment Payment, bool MovesBank)> pieces, int settled, DateTimeOffset now, CancellationToken cancellationToken) {
        // ponytail: split plans stay display-only until the split-allocation ticket (#61) decides what moves the bank.
        var movingPieces = pieces.Where(piece => piece.MovesBank).Select(piece => (piece.Installment, piece.Payment)).ToList();
        var postedPieces = await CreditorPostingHelper.PostPiecesAsync(ledgerApi, command.BankAccountId, movingPieces, $"Full debt · {creditorName}", now, cancellationToken);
        if(postedPieces.IsFailure) {
            return postedPieces.Error;
        }
        try {
            await context.SaveChangesAsync(cancellationToken);
        } catch {
            await CreditorPostingHelper.ReverseAsync(ledgerApi, postedPieces.Value, now);
            throw;
        }
        return settled;
    }
}
