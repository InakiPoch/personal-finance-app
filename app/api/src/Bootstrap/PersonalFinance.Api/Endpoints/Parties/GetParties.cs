using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetParties {
    public static async Task<Ok<PartiesListDto>> Handle(IPartiesApi parties, IFinancingApi financing, IQueryBus queryBus, CancellationToken cancellationToken) {
        var roster = await parties.ListPartiesAsync(new ListPartiesQuery(), cancellationToken);
        var pending = await financing.GetPendingSharesByPartyAsync(new GetPendingSharesByPartyQuery(), cancellationToken);
        var scheduledToYou = pending.Rows.GroupBy(row => row.PartyId).ToDictionary(group => group.Key, group => group.Sum(row => row.ScheduledCount));
        var rows = new List<PartyRowDto>();
        foreach(var party in roster.Rows) {
            var balance = await queryBus.AskAsync(new GetCurrentAccountBalanceQuery(party.Id), cancellationToken);
            var scheduled = await queryBus.AskAsync(new GetPartyScheduledInstallmentsQuery(party.Id), cancellationToken);
            var owedToYou = Positive(balance.Balances);
            var youOwe = Positive(balance.PayableBalances);
            var toYou = scheduledToYou.GetValueOrDefault(party.Id);
            var youOweCount = scheduled.Rows.Count;
            var settledUp = balance.Balances.All(row => row.BalanceMinorUnits == 0)
                && balance.PayableBalances.All(row => row.BalanceMinorUnits == 0)
                && toYou == 0
                && youOweCount == 0;
            rows.Add(new PartyRowDto(party.Id, party.Name, owedToYou, youOwe, toYou, youOweCount, settledUp));
        }
        return TypedResults.Ok(new PartiesListDto(rows));
    }

    private static List<PartyCurrencyBalanceDto> Positive(IReadOnlyList<PartyCurrencyBalance> balances) {
        return balances
            .Where(row => row.BalanceMinorUnits > 0)
            .Select(row => new PartyCurrencyBalanceDto(row.CurrencyCode, row.BalanceMinorUnits))
            .ToList();
    }
}
