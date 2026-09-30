using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Application.Queries.Shared;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCardClosingSchedule;

internal sealed class GetCardClosingScheduleHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<GetCardClosingScheduleQuery, CardClosingScheduleResponse> {
    public async Task<CardClosingScheduleResponse> HandleAsync(GetCardClosingScheduleQuery query, CancellationToken cancellationToken) {
        var card = await context.CreditCards
            .Include(candidate => candidate.ClosingOverrides)
            .FirstOrDefaultAsync(candidate => candidate.Id == query.CardId, cancellationToken);
        if(card is null) {
            return new CardClosingScheduleResponse(false, query.CardId, []);
        }
        var lockedCycles = (await context.MonthlyStatements
            .Where(statement => statement.CardId == query.CardId)
            .Select(statement => new { statement.CycleYear, statement.CycleMonth })
            .ToListAsync(cancellationToken))
            .Select(statement => (statement.CycleYear, statement.CycleMonth))
            .ToHashSet();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var first = CardClosingScheduleHelper.FirstOpenCycle(today, lockedCycles);
        var rows = Enumerable.Range(0, Math.Max(query.Months, 0))
            .Select(offset => first.AddMonths(offset))
            .Select(cycle => new CardClosingScheduleRow(
                cycle.Year,
                cycle.Month,
                card.ClosingDateOf(cycle),
                card.HasOverrideFor(cycle),
                lockedCycles.Contains((cycle.Year, cycle.Month))))
            .ToList();
        return new CardClosingScheduleResponse(true, query.CardId, rows);
    }
}
