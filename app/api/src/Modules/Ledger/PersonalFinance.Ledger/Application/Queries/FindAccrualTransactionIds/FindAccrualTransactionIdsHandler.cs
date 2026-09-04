using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Application.Queries.FindAccrualTransactionIds;

internal sealed class FindAccrualTransactionIdsHandler(LedgerDbContext context)
    : IQueryHandler<FindAccrualTransactionIdsQuery, AccrualTransactionIdsResponse> {
    public async Task<AccrualTransactionIdsResponse> HandleAsync(FindAccrualTransactionIdsQuery query, CancellationToken cancellationToken) {
        if(query.InstallmentReferenceIds.Count == 0) {
            return new AccrualTransactionIdsResponse(new Dictionary<Guid, Guid>());
        }
        var wanted = query.InstallmentReferenceIds.ToHashSet();
        var accruals = await context.Transactions
            .Where(transaction => transaction.OriginalTransactionId == null && transaction.InstallmentReference != null)
            .Select(transaction => new { transaction.Id, Reference = transaction.InstallmentReference })
            .ToListAsync(cancellationToken);
        var map = accruals
            .Where(accrual => accrual.Reference is not null && wanted.Contains(accrual.Reference.Value))
            .GroupBy(accrual => accrual.Reference!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);
        return new AccrualTransactionIdsResponse(map);
    }
}
