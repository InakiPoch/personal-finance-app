using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Queries.GetCurrentAccountBalance;

internal sealed class GetCurrentAccountBalanceHandler(PartiesDbContext context, ILedgerApi ledger) : IQueryHandler<GetCurrentAccountBalanceQuery, CurrentAccountBalanceResponse> {
    public async Task<CurrentAccountBalanceResponse> HandleAsync(GetCurrentAccountBalanceQuery query, CancellationToken cancellationToken) {
        var party = await context.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.PartyId, cancellationToken);
        if(party is null) {
            return new CurrentAccountBalanceResponse(query.PartyId, string.Empty, 0);
        }
        var balances = await ledger.GetAccountBalanceAsync(new GetAccountBalanceQuery(party.ReceivableAccountId), cancellationToken);
        var balance = balances.FirstOrDefault(candidate => candidate.Currency == Currency.Reference);
        return new CurrentAccountBalanceResponse(party.Id, party.Name, balance.MinorUnits);
    }
}
