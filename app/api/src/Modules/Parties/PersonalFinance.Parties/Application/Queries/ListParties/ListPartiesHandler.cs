using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Application.Queries.ListParties;

internal sealed class ListPartiesHandler(PartiesDbContext context) : IQueryHandler<ListPartiesQuery, ListPartiesResponse> {
    public async Task<ListPartiesResponse> HandleAsync(ListPartiesQuery query, CancellationToken cancellationToken) {
        var parties = await context.Parties
            .AsNoTracking()
            .Select(party => new { party.Id, party.Name })
            .ToListAsync(cancellationToken);
        var rows = parties
            .OrderBy(party => party.Name, StringComparer.OrdinalIgnoreCase)
            .Select(party => new PartyRow(party.Id, party.Name))
            .ToList();
        return new ListPartiesResponse(rows);
    }
}
