using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.PostTransaction;

/// <summary>
/// Turns a <see cref="PostTransactionCommand"/> into a posted <see cref="Transaction"/>.
/// </summary>
internal sealed class PostTransactionHandler(TransactionWriter writer) : ICommandHandler<PostTransactionCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PostTransactionCommand command, CancellationToken cancellationToken) {
        var validation = PostTransactionValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var lines = command.Lines
            .Select(line => new EntryDraft(line.AccountId, line.Direction, line.Amount))
            .ToList();
        var splitReference = command.SplitReferenceId is { } splitId ? new SplitReference(splitId) : null;
        var installmentReference = command.InstallmentReferenceId is { } installmentId ? new InstallmentReference(installmentId) : null;
        var subscriptionReference = command.SubscriptionReferenceId is { } subscriptionId ? new SubscriptionReference(subscriptionId) : null;
        var transaction = Transaction.Post(lines, command.PostedOnUtc, splitReference, installmentReference, subscriptionReference, command.Description?.Trim());
        if(transaction.IsFailure) {
            return transaction.Error;
        }
        return await writer.PersistAsync(transaction.Value, cancellationToken);
    }
}
