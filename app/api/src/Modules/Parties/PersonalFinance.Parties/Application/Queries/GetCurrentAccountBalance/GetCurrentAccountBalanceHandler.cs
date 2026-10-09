using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Application.Queries.GetCurrentAccountBalance;

internal sealed class GetCurrentAccountBalanceHandler(PartiesDbContext context, ILedgerApi ledger) : IQueryHandler<GetCurrentAccountBalanceQuery, CurrentAccountBalanceResponse> {
    public async Task<CurrentAccountBalanceResponse> HandleAsync(GetCurrentAccountBalanceQuery query, CancellationToken cancellationToken) {
        var party = await context.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.PartyId, cancellationToken);
        if(party is null) {
            return new CurrentAccountBalanceResponse(query.PartyId, string.Empty, [], []);
        }
        var balances = await ledger.GetAccountBalanceAsync(new GetAccountBalanceQuery(party.ReceivableAccountId), cancellationToken);
        var rows = balances
            .Select(balance => new PartyCurrencyBalance(balance.Currency.Code, balance.MinorUnits))
            .ToList();
        var payable = await ledger.GetAccountBalanceAsync(new GetAccountBalanceQuery(party.PayableAccountId), cancellationToken);
        var payableRows = payable
            .Select(balance => new PartyCurrencyBalance(balance.Currency.Code, balance.MinorUnits))
            .ToList();
        return new CurrentAccountBalanceResponse(party.Id, party.Name, rows, payableRows);
    }
}
