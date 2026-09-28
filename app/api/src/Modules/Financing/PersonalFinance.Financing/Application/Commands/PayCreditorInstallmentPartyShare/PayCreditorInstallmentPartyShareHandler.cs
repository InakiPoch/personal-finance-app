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
/// Settles the party's receivable into a bank account and records that share as a payment on the installment.
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
        await context.SaveChangesAsync(cancellationToken);
        return applied.Value.Id;
    }
}
