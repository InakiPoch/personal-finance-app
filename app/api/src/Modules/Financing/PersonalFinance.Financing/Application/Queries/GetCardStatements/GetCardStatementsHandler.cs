using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCardStatements;

internal sealed class GetCardStatementsHandler(FinancingDbContext context) : IQueryHandler<GetCardStatementsQuery, CardStatementsResponse> {
    public async Task<CardStatementsResponse> HandleAsync(GetCardStatementsQuery query, CancellationToken cancellationToken) {
        var cardName = await context.CreditCards
            .Where(card => card.Id == query.CardId)
            .Select(card => card.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";
        var statements = await context.MonthlyStatements
            .Where(statement => statement.CardId == query.CardId)
            .Select(statement => new {
                statement.Id,
                statement.CardId,
                statement.CycleYear,
                statement.CycleMonth,
                statement.AmountDue,
                statement.PaidOnUtc
            })
            .ToListAsync(cancellationToken);
        var rows = statements
            .OrderBy(statement => statement.CycleYear)
            .ThenBy(statement => statement.CycleMonth)
            .Select(statement => new CardStatementRow(
                statement.Id,
                statement.CardId,
                cardName,
                statement.CycleYear,
                statement.CycleMonth,
                statement.AmountDue.MinorUnits,
                statement.PaidOnUtc is not null,
                statement.PaidOnUtc,
                statement.AmountDue.Currency.Code))
            .ToList();
        return new CardStatementsResponse(rows);
    }
}
