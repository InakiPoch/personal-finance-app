using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayStatement;

/// <summary>
/// Pays a closed statement in full from a bank account, posting <c>Dr CardLiability / Cr Bank</c> to the ledger for the amount due (D2).
/// </summary>
internal sealed class PayStatementHandler(FinancingDbContext context, ILedgerApi ledger) : ICommandHandler<PayStatementCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PayStatementCommand command, CancellationToken cancellationToken) {
        var validation = PayStatementValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var statement = await context.MonthlyStatements
            .FirstOrDefaultAsync(candidate => candidate.Id == command.StatementId, cancellationToken);
        if(statement is null) {
            return FinancingErrors.StatementNotFound;
        }
        if(statement.IsPaid) {
            return FinancingErrors.StatementAlreadyPaid;
        }
        var card = await context.CreditCards
            .FirstOrDefaultAsync(candidate => candidate.Id == statement.CardId, cancellationToken);
        if(card is null) {
            return FinancingErrors.CardNotFound;
        }
        // TODO(Phase 4): net card.CarriedCreditBalance before posting (D12).
        var posting = await ledger.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(card.LiabilityAccountId, DebitOrCredit.Debit, statement.AmountDue),
                    new PostTransactionLine(command.BankAccountId, DebitOrCredit.Credit, statement.AmountDue)
                ],
                command.PaidOnUtc
            ),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        var paid = statement.MarkPaid(command.PaidOnUtc);
        if(paid.IsFailure) {
            return paid.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return statement.Id;
    }
}
