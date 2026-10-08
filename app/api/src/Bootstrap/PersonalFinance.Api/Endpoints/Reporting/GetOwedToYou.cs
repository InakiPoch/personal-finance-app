using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Reporting;

/// <summary>
/// Dashboard "Owed to you". Composed in the host so each module keeps its boundary: Reporting supplies the
/// receivable balances as of the month end (views only), Financing supplies the Scheduled shares due by then.
/// </summary>
public static class GetOwedToYou {
    public static async Task<Ok<OwedToYouDto>> Handle(string month, DateOnly? today, IQueryBus queryBus, CancellationToken cancellationToken) {
        var requested = MonthQueryHelper.Parse(month);
        var current = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var balances = await queryBus.AskAsync(new ReceivableBalancesAsOfQuery(requested), cancellationToken);
        var totals = balances.Rows.ToDictionary(row => (row.PartyId, row.CurrencyCode), row => row.BalanceMinorUnits);
        // A past month never includes shares still waiting to be charged: they were not owed yet.
        if(requested.Year * 12 + requested.Month >= current.Year * 12 + current.Month) {
            var scheduled = await queryBus.AskAsync(new GetPendingSharesByPartyQuery(requested), cancellationToken);
            foreach(var row in scheduled.Rows) {
                var key = (row.PartyId, row.CurrencyCode);
                totals[key] = totals.GetValueOrDefault(key) + row.ScheduledTotalMinorUnits;
            }
        }
        var names = (await queryBus.AskAsync(new ListPartiesQuery(), cancellationToken)).Rows.ToDictionary(row => row.Id, row => row.Name);
        var rows = totals
            .Where(pair => pair.Value > 0 && names.ContainsKey(pair.Key.PartyId))
            .Select(pair => new OwedToYouRowDto(pair.Key.PartyId, names[pair.Key.PartyId], pair.Key.CurrencyCode, pair.Value))
            .OrderBy(row => row.PartyName, StringComparer.Ordinal)
            .ThenBy(row => row.CurrencyCode, StringComparer.Ordinal)
            .ToList();
        return TypedResults.Ok(new OwedToYouDto(rows));
    }
}
