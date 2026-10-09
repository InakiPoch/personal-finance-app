using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Helpers;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Reporting.Reports;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetYouOwe {
    public static async Task<Ok<YouOweDto>> Handle(string month, DateOnly? today, IQueryBus queryBus, TimeProvider timeProvider, CancellationToken cancellationToken) {
        var requested = MonthQueryHelper.Parse(month);
        var current = today ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var balances = await queryBus.AskAsync(new PayableBalancesAsOfQuery(requested), cancellationToken);
        var names = (await queryBus.AskAsync(new ListPartiesQuery(), cancellationToken)).Rows.ToDictionary(row => row.Id, row => row.Name);
        var scheduled = new List<(Guid PartyId, string CurrencyCode, long AmountMinorUnits)>();
        if(OwedToYouHelper.IncludesScheduled(requested, current)) {
            foreach(var partyId in names.Keys) {
                var response = await queryBus.AskAsync(new GetPartyScheduledInstallmentsQuery(partyId), cancellationToken);
                scheduled.AddRange(response.Rows
                    .Where(row => row.CycleYear * 12 + row.CycleMonth <= requested.MonthOrdinal())
                    .Select(row => (partyId, row.CurrencyCode, row.ShareMinorUnits)));
            }
        }
        return TypedResults.Ok(new YouOweDto(YouOweHelper.Merge(balances, scheduled, names)));
    }
}
