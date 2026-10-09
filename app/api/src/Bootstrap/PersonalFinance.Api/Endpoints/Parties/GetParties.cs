using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetParties {
    public static async Task<Ok<PartiesListDto>> Handle(IPartiesApi parties, IFinancingApi financing, IQueryBus queryBus, TimeProvider timeProvider, CancellationToken cancellationToken) {
        var roster = await parties.ListPartiesAsync(new ListPartiesQuery(), cancellationToken);
        var pending = await financing.GetPendingSharesByPartyAsync(new GetPendingSharesByPartyQuery(), cancellationToken);
        var scheduledToYou = pending.Rows.GroupBy(row => row.PartyId).ToDictionary(group => group.Key, group => group.Sum(row => row.ScheduledCount));
        var owedByParty = (await queryBus.AskAsync(new GetDebtByPartyQuery(), cancellationToken)).Rows
            .Where(row => row.NetBalanceMinorUnits > 0)
            .ToLookup(row => row.PartyId, row => new PartyCurrencyBalanceDto(row.CurrencyCode, row.NetBalanceMinorUnits));
        var payableMonth = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var oweByParty = (await queryBus.AskAsync(new PayableBalancesAsOfQuery(payableMonth), cancellationToken)).Rows
            .ToLookup(row => row.PartyId, row => new PartyCurrencyBalanceDto(row.CurrencyCode, row.BalanceMinorUnits));
        var scheduledYouOwe = (await queryBus.AskAsync(new GetPartyScheduledInstallmentsQuery(), cancellationToken)).Rows
            .GroupBy(row => row.PartyId)
            .ToDictionary(group => group.Key, group => group.Count());
        var rows = roster.Rows.Select(party => {
            var owedToYou = owedByParty[party.Id].ToList();
            var youOwe = oweByParty[party.Id].ToList();
            var toYou = scheduledToYou.GetValueOrDefault(party.Id);
            var youOweCount = scheduledYouOwe.GetValueOrDefault(party.Id);
            var settledUp = owedToYou.Count == 0 && youOwe.Count == 0 && toYou == 0 && youOweCount == 0;
            return new PartyRowDto(party.Id, party.Name, owedToYou, youOwe, toYou, youOweCount, settledUp);
        }).ToList();
        return TypedResults.Ok(new PartiesListDto(rows));
    }
}
