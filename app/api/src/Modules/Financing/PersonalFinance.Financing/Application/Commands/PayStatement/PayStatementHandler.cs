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
/// Pays a closed statement by settling every accrued, non-reversed, still-unpaid installment it holds in one posting. The charge is the sum of
/// those installments — not the stored <see cref="MonthlyStatement.AmountDue"/>, which is only ever incremented and still carries reversed cuotas
/// whose ledger liability was already stornoed.
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
        var statementInstallments = await context.Set<Installment>()
            .Where(installment => installment.StatementId == command.StatementId)
            .ToListAsync(cancellationToken);
        var settleable = statementInstallments
            .Where(installment => installment.IsAccrued && !installment.IsReversed && !installment.IsPaid)
            .ToList();
        var payable = settleable.Aggregate(
            Money.Zero(Currency.Reference),
            (running, installment) => running + installment.Amount
        );
        if(payable.MinorUnits == 0) {
            return FinancingErrors.StatementAlreadyPaid;
        }
        var netted = StatementPaymentCalculator.Build(
            card.CarriedCreditBalance,
            payable,
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
        foreach(var installment in settleable) {
            var installmentPaid = installment.MarkPaid(command.PaidOnUtc);
            if(installmentPaid.IsFailure) {
                return installmentPaid.Error;
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
