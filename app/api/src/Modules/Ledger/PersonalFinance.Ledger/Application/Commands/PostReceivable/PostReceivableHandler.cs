using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.PostReceivable;

/// <summary>
/// Records a shared expense the holder fronted in full:
/// <c>Dr Expense (own share) + Dr Receivable (other party's share) / Cr Funding (total)</c>.
/// </summary>
internal sealed class PostReceivableHandler(TransactionWriter writer) : ICommandHandler<PostReceivableCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PostReceivableCommand command, CancellationToken cancellationToken) {
        var validation = validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var lines = new List<EntryDraft> {
            new(command.ExpenseAccountId, DebitOrCredit.Debit, command.OwnShare),
            new(command.ReceivableAccountId, DebitOrCredit.Debit, command.ReceivableShare),
            new(command.FundingAccountId, DebitOrCredit.Credit, command.OwnShare + command.ReceivableShare)
        };
        var splitReference = command.SplitReferenceId is { } splitId ? new SplitReference(splitId) : null;
        var transaction = Transaction.Post(lines, command.PostedOnUtc, splitReference);
        if(transaction.IsFailure) {
            return transaction.Error;
        }
        return await writer.PersistAsync(transaction.Value, cancellationToken);
    }

    private static Result validate(PostReceivableCommand command) {
        if(command.ExpenseAccountId == Guid.Empty
            || command.ReceivableAccountId == Guid.Empty
            || command.FundingAccountId == Guid.Empty) {
            return Result.Failure(LedgerErrors.AccountNotFound);
        }
        if(command.OwnShare.Currency != command.ReceivableShare.Currency) {
            return Result.Failure(LedgerErrors.MixedCurrency);
        }
        if(command.OwnShare.MinorUnits <= 0 || command.ReceivableShare.MinorUnits <= 0) {
            return Result.Failure(LedgerErrors.NonPositiveEntryAmount);
        }
        return Result.Success();
    }
}
