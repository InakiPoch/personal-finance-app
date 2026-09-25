using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Queries.GetCardLiability;

/// <summary>
/// Credit-positive sum of posted entries over <c>CardLiability</c> accounts.
/// </summary>
internal sealed class GetCardLiabilityHandler(LedgerDbContext context) : IQueryHandler<GetCardLiabilityQuery, IReadOnlyList<Money>> {
    public async Task<IReadOnlyList<Money>> HandleAsync(GetCardLiabilityQuery query, CancellationToken cancellationToken) {
        var cardAccountIds = await context.Accounts
            .Where(account => account.Kind == AccountKind.CardLiability)
            .Where(account => query.CardAccountId == null || account.Id == query.CardAccountId)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);
        if(cardAccountIds.Count == 0) {
            return [];
        }
        var entries = await context.Set<Entry>()
            .Where(entry => cardAccountIds.Contains(entry.AccountId))
            .Select(entry => new { entry.Direction, entry.Amount })
            .ToListAsync(cancellationToken);
        return entries
            .GroupBy(entry => entry.Amount.Currency)
            .Select(group => {
                var debitPositive = group.Sum(entry =>
                    entry.Direction == DebitOrCredit.Debit ? entry.Amount.MinorUnits : -entry.Amount.MinorUnits);
                return Money.FromMinorUnits(-debitPositive, group.Key);
            })
        .ToList();
    }
}
