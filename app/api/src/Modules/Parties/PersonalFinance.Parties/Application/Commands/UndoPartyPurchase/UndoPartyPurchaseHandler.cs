using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.UndoPartyPurchase;

internal sealed class UndoPartyPurchaseHandler(PartiesDbContext context, ILedgerApi ledger, TimeProvider timeProvider) : ICommandHandler<UndoPartyPurchaseCommand> {
    private const string alreadyReversedCode = "Ledger.TransactionAlreadyReversed";

    public async Task<Result> HandleAsync(UndoPartyPurchaseCommand command, CancellationToken cancellationToken) {
        var purchase = await context.PartyPurchases
            .Include(candidate => candidate.Installments)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.PurchaseId && candidate.PartyId == command.PartyId, cancellationToken);
        if(purchase is null) {
            return Result.Failure(PartiesErrors.PurchaseNotFound);
        }
        var wasCancelled = purchase.IsCancelled;
        if(!wasCancelled) {
            purchase.Cancel();
            await context.SaveChangesAsync(cancellationToken);
        }
        var reversedAny = false;
        foreach(var transactionId in purchase.Installments.Where(installment => installment.LedgerTransactionId is not null).Select(installment => installment.LedgerTransactionId!.Value)) {
            var reversed = await PartyHandlerHelper.ReverseAsync(ledger, transactionId, timeProvider);
            if(reversed.IsSuccess) {
                reversedAny = true;
            } else if(reversed.Error.Code != alreadyReversedCode) {
                return Result.Failure(reversed.Error);
            }
        }
        return wasCancelled && !reversedAny ? Result.Failure(PartiesErrors.PurchaseAlreadyUndone) : Result.Success();
    }
}
