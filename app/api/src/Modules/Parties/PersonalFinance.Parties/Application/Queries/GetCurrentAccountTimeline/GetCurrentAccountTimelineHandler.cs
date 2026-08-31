using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Application.Queries.GetCurrentAccountTimeline;

internal sealed class GetCurrentAccountTimelineHandler(PartiesDbContext context) : IQueryHandler<GetCurrentAccountTimelineQuery, CurrentAccountTimelineResponse> {
    public async Task<CurrentAccountTimelineResponse> HandleAsync(GetCurrentAccountTimelineQuery query, CancellationToken cancellationToken) {
        var movements = await context.CurrentAccountTimeline
            .AsNoTracking()
            .Where(entry => entry.PartyId == query.PartyId)
            .OrderBy(entry => entry.MovementOnUtc)
            .Select(entry => new {
                entry.MovementOnUtc,
                entry.Description,
                entry.DeltaMinorUnits,
                entry.RunningBalanceMinorUnits
            })
            .ToListAsync(cancellationToken);
        var rows = movements
            .Select(entry => new CurrentAccountTimelineRow(
                entry.MovementOnUtc,
                entry.Description,
                entry.DeltaMinorUnits,
                entry.RunningBalanceMinorUnits)
            )
            .ToList();
        return new CurrentAccountTimelineResponse(rows);
    }
}
