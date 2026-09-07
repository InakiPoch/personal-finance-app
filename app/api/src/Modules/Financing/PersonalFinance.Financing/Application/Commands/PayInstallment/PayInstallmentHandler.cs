using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayInstallment;

/// <summary>
/// Pays one accrued, non-reversed, still-unpaid credit-card installment in full.
/// </summary>
internal sealed class PayInstallmentHandler(FinancingDbContext context, ILedgerApi ledger) : ICommandHandler<PayInstallmentCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PayInstallmentCommand command, CancellationToken cancellationToken) {
        var validation = PayInstallmentValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var installment = await context.Set<Installment>()
            .FirstOrDefaultAsync(candidate => candidate.Id == command.InstallmentId, cancellationToken);
        if(installment is null) {
            return FinancingErrors.InstallmentNotFound;
        }
        if(installment.IsReversed) {
            return FinancingErrors.InstallmentAlreadyReversed;
        }
        if(installment.IsPaid) {
            return FinancingErrors.InstallmentAlreadyPaid;
        }
        if(!installment.IsAccrued || installment.StatementId is null) {
            return FinancingErrors.InstallmentNotAccrued;
        }
        var statement = await context.MonthlyStatements
            .FirstOrDefaultAsync(candidate => candidate.Id == installment.StatementId.Value, cancellationToken);
        if(statement is null) {
            return FinancingErrors.StatementNotFound;
        }
        var card = await context.CreditCards
            .FirstOrDefaultAsync(candidate => candidate.Id == statement.CardId, cancellationToken);
        if(card is null) {
            return FinancingErrors.CardNotFound;
        }
        var lines = new List<PostTransactionLine> {
            new(card.LiabilityAccountId, DebitOrCredit.Debit, installment.Amount),
            new(command.BankAccountId, DebitOrCredit.Credit, installment.Amount)
        };
        var posting = await ledger.PostTransactionAsync(
            new PostTransactionCommand(lines, command.PaidOnUtc),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        var installmentPaid = installment.MarkPaid(command.PaidOnUtc);
        if(installmentPaid.IsFailure) {
            return installmentPaid.Error;
        }
        var statementInstallments = await context.Set<Installment>()
            .Where(candidate => candidate.StatementId == statement.Id)
            .ToListAsync(cancellationToken);
        if(!statement.IsPaid && statement.IsFullyPaidBy(statementInstallments)) {
            var statementPaid = statement.MarkPaid(command.PaidOnUtc);
            if(statementPaid.IsFailure) {
                return statementPaid.Error;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return installment.Id;
    }
}
