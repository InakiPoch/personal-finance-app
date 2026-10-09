using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Application.Queries.GetCurrentAccountTimeline;

internal sealed class GetCurrentAccountTimelineHandler(PartiesDbContext context) : IQueryHandler<GetCurrentAccountTimelineQuery, CurrentAccountTimelineResponse> {
    public async Task<CurrentAccountTimelineResponse> HandleAsync(GetCurrentAccountTimelineQuery query, CancellationToken cancellationToken) {
        // SQLite cannot ORDER BY a DateTimeOffset column; the view's window function already
        // computes the running balance in PostedOnUtc order, so we sort the rows for display client-side.
        var movements = await context.CurrentAccountTimeline
            .AsNoTracking()
            .Where(entry => entry.PartyId == query.PartyId)
            .Select(entry => new {
                entry.EntryId,
                entry.TransactionId,
                entry.MovementOnUtc,
                entry.Description,
                entry.DeltaMinorUnits,
                entry.RunningBalanceMinorUnits,
                entry.CurrencyCode
            })
            .ToListAsync(cancellationToken);
        var rows = movements
            .OrderBy(entry => entry.MovementOnUtc)
            .ThenBy(entry => entry.EntryId.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CurrentAccountTimelineRow(
                entry.TransactionId,
                entry.MovementOnUtc,
                entry.Description,
                entry.DeltaMinorUnits,
                entry.RunningBalanceMinorUnits,
                entry.CurrencyCode)
            )
            .ToList();
        return new CurrentAccountTimelineResponse(rows);
    }
}
