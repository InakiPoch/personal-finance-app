using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;

internal sealed class LinkPaymentPlanSplitHandler(FinancingDbContext context, ILedgerApi ledger) : ICommandHandler<LinkPaymentPlanSplitCommand> {
    public async Task<Result> HandleAsync(LinkPaymentPlanSplitCommand command, CancellationToken cancellationToken) {
        var validation = LinkPaymentPlanSplitValidator.Validate(command);
        if(validation.IsFailure) {
            return validation;
        }
        var plan = await context.PaymentPlans
            .Include(candidate => candidate.SplitParticipants)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.PaymentPlanId, cancellationToken);
        if(plan is null) {
            return Result.Failure(FinancingErrors.PaymentPlanNotFound);
        }
        if(plan.SplitReferenceId is not null) {
            return Result.Success();
        }
        var receivableAccountsByParty = command.PartyReceivables
            .ToDictionary(receivable => receivable.PartyId, receivable => receivable.ReceivableAccountId);
        var linked = plan.LinkSplit(command.SplitReferenceId, receivableAccountsByParty);
        if(linked.IsFailure) {
            return linked;
        }
        if(plan.CardId is null && plan.CreditorPayableAccountId is null) {
            var account = await ledger.CreateAccountAsync(
                new CreateAccountCommand(
                    $"Payable to creditor — {plan.Description}",
                    AccountType.Liability,
                    AccountKind.CreditorPayable,
                    OwnerReferenceId: plan.Id
                ),
                cancellationToken
            );
            if(account.IsFailure) {
                return account;
            }
            plan.AssignCreditorPayableAccount(account.Value);
        }
        if(plan.CardId is null && plan.SplitParticipants.Count > 0) {
            var (lines, _) = CreditorSplitReceivableCalculator.BuildLines(
                plan.Total, plan.SplitParticipants, plan.CreditorPayableAccountId!.Value
            );
            if(lines.Count > 0) {
                var posting = await ledger.PostTransactionAsync(
                    new PostTransactionCommand(
                        lines,
                        plan.PurchaseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        SplitReferenceId: command.SplitReferenceId,
                        Description: "Creditor-financed split"
                    ),
                    cancellationToken
                );
                if(posting.IsFailure) {
                    return posting;
                }
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
