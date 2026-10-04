using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.RecordIncome;

/// <summary>
/// Records real money arriving in a Bank/Cash account: one balanced <c>Dr target / Cr Income</c> transaction, never in the future.
/// </summary>
internal sealed class RecordIncomeHandler(LedgerDbContext context, TransactionWriter writer, TimeProvider timeProvider) : ICommandHandler<RecordIncomeCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RecordIncomeCommand command, CancellationToken cancellationToken) {
        var validation = RecordIncomeValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        if(command.ReceivedOn > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)) {
            return LedgerErrors.IncomeDateInFuture;
        }
        var target = await context.Accounts
            .FirstOrDefaultAsync(account => account.Id == command.TargetAccountId, cancellationToken);
        if(target is null) {
            return LedgerErrors.AccountNotFound;
        }
        if(target.Kind is not (AccountKind.Bank or AccountKind.Cash)) {
            return LedgerErrors.SourceAccountNotSpendable;
        }
        var income = await IncomeAccountProvisioning.GetOrCreateAsync(context, cancellationToken);
        if(income.IsFailure) {
            return income.Error;
        }
        var amount = Money.FromMinorUnits(command.AmountMinorUnits, Currency.FromCode(command.CurrencyCode));
        var postedOnUtc = command.ReceivedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var transaction = Transaction.Post(
            [
                new EntryDraft(target.Id, DebitOrCredit.Debit, amount),
                new EntryDraft(income.Value, DebitOrCredit.Credit, amount)
            ],
            postedOnUtc,
            description: command.Description.Trim());
        if(transaction.IsFailure) {
            return transaction.Error;
        }
        return await writer.PersistAsync(transaction.Value, cancellationToken);
    }
}
