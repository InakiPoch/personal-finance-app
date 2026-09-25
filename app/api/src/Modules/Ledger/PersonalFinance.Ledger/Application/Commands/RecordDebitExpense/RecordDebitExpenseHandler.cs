using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.RecordDebitExpense;

/// <summary>
/// Records a debit or cash expense. Non-split: one balanced <c>Dr category / Cr source</c>
/// transaction. Split: delegates to Parties' <see cref="RegisterSharedExpenseCommand"/>, which
/// posts <c>Dr category(holder) + Dr receivable_k(share_k) / Cr source(total)</c> and tracks the <c>ExpenseSplit</c>
/// </summary>
internal sealed class RecordDebitExpenseHandler(LedgerDbContext context, TransactionWriter writer, IPartiesApi parties)
    : ICommandHandler<RecordDebitExpenseCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RecordDebitExpenseCommand command, CancellationToken cancellationToken) {
        var validation = RecordDebitExpenseValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var source = await context.Accounts
            .FirstOrDefaultAsync(account => account.Id == command.SourceAccountId, cancellationToken);
        if(source is null) {
            return LedgerErrors.AccountNotFound;
        }
        if(source.Kind is not (AccountKind.Bank or AccountKind.Cash)) {
            return LedgerErrors.SourceAccountNotSpendable;
        }
        var category = await ExpenseCategoryProvisioning.GetOrCreateAsync(context, command.CategoryName, cancellationToken);
        if(category.IsFailure) {
            return category.Error;
        }
        var total = Money.FromMinorUnits(command.AmountMinorUnits, Currency.FromCode(command.CurrencyCode));
        var postedOnUtc = command.PurchaseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if(command.Split is { Count: > 0 }) {
            var participants = command.Split
                .Select(participant => new SharedExpenseParticipant(participant.PartyId, participant.Weight))
                .ToList();
            return await parties.RegisterSharedExpenseAsync(
                new RegisterSharedExpenseCommand(
                    command.Description.Trim(),
                    command.AmountMinorUnits,
                    category.Value,
                    source.Id,
                    postedOnUtc,
                    participants),
                cancellationToken);
        }
        var transaction = Transaction.Post(
            [
                new EntryDraft(category.Value, DebitOrCredit.Debit, total),
                new EntryDraft(source.Id, DebitOrCredit.Credit, total)
            ],
            postedOnUtc);
        if(transaction.IsFailure) {
            return transaction.Error;
        }
        return await writer.PersistAsync(transaction.Value, cancellationToken);
    }
}
