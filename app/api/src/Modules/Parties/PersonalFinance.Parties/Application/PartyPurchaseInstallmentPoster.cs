using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application;

/// <summary>
/// Posts every uncancelled party purchase installment whose month has started and that is not posted yet; reverses a posting whose save fails or whose purchase was cancelled meanwhile.
/// </summary>
internal sealed class PartyPurchaseInstallmentPoster(PartiesDbContext context, ILedgerApi ledger, TimeProvider timeProvider, ILogger<PartyPurchaseInstallmentPoster> logger) {
    public async Task PostDueAsync(DateOnly today, Guid? purchaseId, CancellationToken cancellationToken) {
        var purchases = await context.PartyPurchases
            .Include(purchase => purchase.Installments)
            .Where(purchase => !purchase.IsCancelled)
            .Where(purchase => purchaseId == null || purchase.Id == purchaseId)
            .Where(purchase => purchase.Installments.Any(installment => installment.LedgerTransactionId == null && installment.DueOn <= today))
            .ToListAsync(cancellationToken);
        foreach(var purchase in purchases) {
            var party = await context.Parties.FirstAsync(candidate => candidate.Id == purchase.PartyId, cancellationToken);
            var category = await ledger.GetOrCreateExpenseCategoryAsync(new GetOrCreateExpenseCategoryCommand(purchase.CategoryName), cancellationToken);
            if(category.IsFailure) {
                logger.LogWarning("Skipping party purchase {PurchaseId}: category failed ({ErrorCode}).", purchase.Id, category.Error.Code);
                continue;
            }
            foreach(var installment in purchase.Installments.Where(candidate => candidate.LedgerTransactionId is null && candidate.DueOn <= today).OrderBy(candidate => candidate.Number)) {
                var amount = Money.FromMinorUnits(installment.AmountMinorUnits, installment.Currency);
                var posted = await ledger.PostTransactionAsync(
                    new PostTransactionCommand(
                        [
                            new PostTransactionLine(category.Value, DebitOrCredit.Debit, amount),
                            new PostTransactionLine(party.PayableAccountId, DebitOrCredit.Credit, amount)
                        ],
                        installment.DueOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        Description: purchase.Description
                    ),
                    cancellationToken
                );
                if(posted.IsFailure) {
                    logger.LogWarning("Skipping installment {Number} of party purchase {PurchaseId}: ledger post failed ({ErrorCode}).", installment.Number, purchase.Id, posted.Error.Code);
                    continue;
                }
                installment.MarkPosted(posted.Value);
                try {
                    await context.SaveChangesAsync(cancellationToken);
                } catch {
                    await PartyHandlerHelper.ReverseAsync(ledger, posted.Value, timeProvider);
                    throw;
                }
                var cancelled = await context.PartyPurchases
                    .AsNoTracking()
                    .AnyAsync(candidate => candidate.Id == purchase.Id && candidate.IsCancelled, cancellationToken);
                if(cancelled) {
                    await PartyHandlerHelper.ReverseAsync(ledger, posted.Value, timeProvider);
                    break;
                }
            }
        }
    }
}
