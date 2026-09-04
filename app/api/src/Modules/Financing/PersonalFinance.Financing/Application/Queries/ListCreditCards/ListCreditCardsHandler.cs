using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.ListCreditCards;

internal sealed class ListCreditCardsHandler(FinancingDbContext context) : IQueryHandler<ListCreditCardsQuery, ListCreditCardsResponse> {
    public async Task<ListCreditCardsResponse> HandleAsync(ListCreditCardsQuery query, CancellationToken cancellationToken) {
        var cards = await context.CreditCards
            .Select(card => new { card.Id, card.Name, card.CutoffDay })
            .ToListAsync(cancellationToken);
        var rows = cards
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => new CreditCardRow(card.Id, card.Name, card.CutoffDay))
            .ToList();
        return new ListCreditCardsResponse(rows);
    }
}
