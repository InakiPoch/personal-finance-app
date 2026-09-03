using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Application.Queries.ListInstrumentAccounts;

internal sealed class ListInstrumentAccountsHandler(LedgerDbContext context) : IQueryHandler<ListInstrumentAccountsQuery, InstrumentAccountsResponse> {
    public async Task<InstrumentAccountsResponse> HandleAsync(ListInstrumentAccountsQuery query, CancellationToken cancellationToken) {
        var accounts = await context.Accounts
            .Where(account => account.Kind == AccountKind.Bank || account.Kind == AccountKind.Cash)
            .Select(account => new { account.Id, account.Name, account.Kind })
            .ToListAsync(cancellationToken);
        var rows = accounts
            .OrderBy(account => account.Name, StringComparer.OrdinalIgnoreCase)
            .Select(account => new InstrumentAccountRow(account.Id, account.Name, account.Kind.ToString()))
            .ToList();
        return new InstrumentAccountsResponse(rows);
    }
}
