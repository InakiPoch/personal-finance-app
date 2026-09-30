using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Application.Queries.Shared;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.ListCreditCards;

internal sealed class ListCreditCardsHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<ListCreditCardsQuery, ListCreditCardsResponse> {
    public async Task<ListCreditCardsResponse> HandleAsync(ListCreditCardsQuery query, CancellationToken cancellationToken) {
        var cards = await context.CreditCards
            .Include(card => card.ClosingOverrides)
            .ToListAsync(cancellationToken);
        var statements = await context.MonthlyStatements
            .Select(statement => new { statement.CardId, statement.CycleYear, statement.CycleMonth })
            .ToListAsync(cancellationToken);
        var lockedByCard = statements
            .GroupBy(statement => statement.CardId)
            .ToDictionary(group => group.Key, group => group.Select(statement => (statement.CycleYear, statement.CycleMonth)).ToHashSet());
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var rows = cards
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => new CreditCardRow(
                card.Id,
                card.Name,
                card.CutoffDay,
                card.ClosingDateOf(CardClosingScheduleHelper.FirstOpenCycle(today, lockedByCard.GetValueOrDefault(card.Id) ?? []))))
            .ToList();
        return new ListCreditCardsResponse(rows);
    }
}
