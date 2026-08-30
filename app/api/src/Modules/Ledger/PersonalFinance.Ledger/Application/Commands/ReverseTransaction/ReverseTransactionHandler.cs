using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.ReverseTransaction;

internal sealed class ReverseTransactionHandler(LedgerDbContext context, TransactionWriter writer) : ICommandHandler<ReverseTransactionCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(ReverseTransactionCommand command, CancellationToken cancellationToken) {
        var validation = ReverseTransactionValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var original = await context.Transactions
            .Include(transaction => transaction.Entries)
            .FirstOrDefaultAsync(transaction => transaction.Id == command.OriginalTransactionId, cancellationToken);
        if(original is null) {
            return LedgerErrors.OriginalTransactionNotFound;
        }
        // TODO(Phase 4): D12 — ask IFinancingApi for a compensating card-credit entry when the
        // reversed installment was already paid (netted against the next statement, never Activo:Banco),
        // and cascade synchronously to IPartiesApi when original.SplitReference is present.
        // Phase 2 is plain storno only.
        var reversal = Transaction.Reverse(original, command.ReversedOnUtc);
        if(reversal.IsFailure) {
            return reversal.Error;
        }
        return await writer.PersistAsync(reversal.Value, cancellationToken);
    }
}
