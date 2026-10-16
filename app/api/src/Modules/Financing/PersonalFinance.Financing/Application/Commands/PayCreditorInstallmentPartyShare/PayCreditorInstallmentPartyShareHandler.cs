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
/// Settles a party's share into a bank account, sends it on to the creditor and records it as a payment.
/// </summary>
internal sealed class PayCreditorInstallmentPartyShareHandler(FinancingDbContext context, TimeProvider timeProvider,
    IPartiesApi partiesApi, ILedgerApi ledgerApi) : ICommandHandler<PayCreditorInstallmentPartyShareCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PayCreditorInstallmentPartyShareCommand command, CancellationToken cancellationToken) {
        if(command.BankAccountId == Guid.Empty) {
            return FinancingErrors.InvalidPaidFromAccount;
        }
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
            await CreditorPostingHelper.ReverseAsync(ledgerApi, [settled.Value], now);
            return applied.Error;
        }
        var creditorName = await CreditorPostingHelper.LoadCreditorNameAsync(context, plan.CreditorId, cancellationToken);
        if(creditorName is null) {
            await CreditorPostingHelper.ReverseAsync(ledgerApi, [settled.Value], now);
            return FinancingErrors.CreditorNotFound;
        }
        var posted = await CreditorPostingHelper.PostPayableAsync(ledgerApi, command.BankAccountId, plan.CreditorPayableAccountId!.Value, Money.FromMinorUnits(share, installment.Currency), CreditorPaymentDescriptionHelper.ForInstallment(plan, installment, creditorName), now, cancellationToken);
        if(posted.IsFailure) {
            await CreditorPostingHelper.ReverseAsync(ledgerApi, [settled.Value], now);
            return posted.Error;
        }
        applied.Value.RecordLedgerTransaction(posted.Value);
        await CreditorPostingHelper.SaveOrReverseAsync(context, ledgerApi, [posted.Value, settled.Value], now, cancellationToken);
        return applied.Value.Id;
    }
}
