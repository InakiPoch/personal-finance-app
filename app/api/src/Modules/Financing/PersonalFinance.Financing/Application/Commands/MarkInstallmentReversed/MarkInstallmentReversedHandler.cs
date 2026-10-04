using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.MarkInstallmentReversed;

internal sealed class MarkInstallmentReversedHandler(FinancingDbContext context) : ICommandHandler<MarkInstallmentReversedCommand> {
    public async Task<Result> HandleAsync(MarkInstallmentReversedCommand command, CancellationToken cancellationToken) {
        var validation = MarkInstallmentReversedValidator.Validate(command);
        if(validation.IsFailure) {
            return validation;
        }
        var row = await (
            from installment in context.Set<Installment>()
            where installment.Id == command.InstallmentId
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            from card in context.CreditCards.Where(candidate => candidate.Id == plan.CardId).DefaultIfEmpty()
            select new { Installment = installment, Card = card }
        ).FirstOrDefaultAsync(cancellationToken);
        if(row is null) {
            return Result.Failure(FinancingErrors.InstallmentNotFound);
        }
        var marked = row.Installment.MarkReversed();
        if(marked.IsFailure) {
            return marked;
        }
        if(command.CompensatingCreditPosted && row.Card is not null) {
            var credit = row.Card.ApplyCredit(Money.FromMinorUnits(command.CreditAmountMinorUnits, Currency.Reference));
            if(credit.IsFailure) {
                return credit;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
