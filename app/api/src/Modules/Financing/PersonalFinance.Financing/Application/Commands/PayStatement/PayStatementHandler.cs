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
/// Pays a closed statement in full. Any carried card credit from a reversed paid installment is netted first, turning the posting into
/// <c>Dr CardLiability (amount due) / Cr Bank (remainder) / Cr CardCredit (credit applied)</c> and retiring the applied credit from the card.
/// With no carried credit it stays the plain <c>Dr CardLiability / Cr Bank</c>.
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
        var netted = StatementPaymentCalculator.Build(
            card.CarriedCreditBalance,
            statement.AmountDue,
            card.LiabilityAccountId,
            command.BankAccountId,
            card.CreditAccountId
        );
        var posting = await ledger.PostTransactionAsync(
            new PostTransactionCommand(netted.Lines, command.PaidOnUtc),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        if(netted.CreditApplied.MinorUnits > 0) {
            var consumed = card.ConsumeCredit(netted.CreditApplied);
            if(consumed.IsFailure) {
                return consumed.Error;
            }
        }
        var paid = statement.MarkPaid(command.PaidOnUtc);
        if(paid.IsFailure) {
            return paid.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return statement.Id;
    }
}
