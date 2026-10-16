using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorInstallmentPartyShare;

/// <summary>
/// Settles the party's receivable into a bank account, sends the same amount on to the creditor
/// (<c>Dr CreditorPayable / Cr Bank</c>, ADR 0018) and records that share as a payment on the installment.
/// </summary>
internal sealed class PayCreditorInstallmentPartyShareHandler(FinancingDbContext context, TimeProvider timeProvider,
    IPartiesApi partiesApi, ILedgerApi ledgerApi) : ICommandHandler<PayCreditorInstallmentPartyShareCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PayCreditorInstallmentPartyShareCommand command, CancellationToken cancellationToken) {
        var installment = await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.InstallmentId, cancellationToken);
        if(installment is null) {
            return FinancingErrors.InstallmentNotFound;
        }
        var plan = await context.PaymentPlans
            .FirstOrDefaultAsync(candidate => candidate.Id == installment.PaymentPlanId, cancellationToken);
        if(plan is null || plan.CreditorId is null) {
            return FinancingErrors.NotACreditorInstallment;
        }
        if(installment.IsReversed) {
            return FinancingErrors.InstallmentAlreadyReversed;
        }
        if(!installment.IsSplitAccrued) {
            return FinancingErrors.PartyShareNotDue;
        }
        var participants = await context.Set<PaymentPlanSplitParticipant>()
            .Where(participant => participant.PaymentPlanId == plan.Id)
            .OrderBy(participant => participant.PartyId)
            .ToListAsync(cancellationToken);
        if(participants.All(participant => participant.PartyId != command.PartyId)) {
            return FinancingErrors.PartyNotInSplit;
        }
        if(installment.Payments.Any(payment => payment.PartyId == command.PartyId)) {
            return FinancingErrors.PartyShareAlreadyPaid;
        }
        var share = CreditorSplitReceivableCalculator.PartyShares(installment.Amount, participants)
            .First(candidate => candidate.PartyId == command.PartyId)
            .ShareMinorUnits;
        if(share > installment.RemainingMinorUnits) {
            return FinancingErrors.PaymentExceedsRemaining;
        }
        var now = timeProvider.GetUtcNow();
        var settled = await partiesApi.SettleCurrentAccountAsync(
            new SettleCurrentAccountCommand(command.PartyId, share, command.BankAccountId, now, installment.Currency.Code),
            cancellationToken);
        if(settled.IsFailure) {
            return settled.Error;
        }
        var applied = installment.ApplyPayment(share, now, command.PartyId, settled.Value);
        if(applied.IsFailure) {
            await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(settled.Value, now), cancellationToken);
            return applied.Error;
        }
        var creditorName = await context.Creditors
            .Where(candidate => candidate.Id == plan.CreditorId)
            .Select(candidate => candidate.Name)
            .FirstAsync(cancellationToken);
        var amount = Money.FromMinorUnits(share, installment.Currency);
        var posted = await ledgerApi.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(plan.CreditorPayableAccountId!.Value, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(command.BankAccountId, DebitOrCredit.Credit, amount)
                ],
                now,
                Description: $"Installment {installment.Sequence}/{plan.InstallmentCount} · {plan.Description} · {creditorName}"
            ),
            cancellationToken
        );
        if(posted.IsFailure) {
            await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(settled.Value, now), CancellationToken.None);
            return posted.Error;
        }
        applied.Value.RecordLedgerTransaction(posted.Value);
        try {
            await context.SaveChangesAsync(cancellationToken);
        } catch {
            await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(posted.Value, now), CancellationToken.None);
            await ledgerApi.ReverseTransactionAsync(new ReverseTransactionCommand(settled.Value, now), CancellationToken.None);
            throw;
        }
        return applied.Value.Id;
    }
}
