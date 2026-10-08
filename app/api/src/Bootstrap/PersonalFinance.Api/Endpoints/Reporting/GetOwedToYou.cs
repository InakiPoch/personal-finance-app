using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Helpers;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetOwedToYou {
    public static async Task<Ok<OwedToYouDto>> Handle(string month, DateOnly? today, IQueryBus queryBus, TimeProvider timeProvider, CancellationToken cancellationToken) {
        var requested = MonthQueryHelper.Parse(month);
        var current = today ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var balances = await queryBus.AskAsync(new ReceivableBalancesAsOfQuery(requested), cancellationToken);
        var scheduled = OwedToYouHelper.IncludesScheduled(requested, current)
            ? await queryBus.AskAsync(new GetPendingSharesByPartyQuery(requested), cancellationToken)
            : null;
        var names = (await queryBus.AskAsync(new ListPartiesQuery(), cancellationToken)).Rows.ToDictionary(row => row.Id, row => row.Name);
        return TypedResults.Ok(new OwedToYouDto(OwedToYouHelper.Merge(balances, scheduled, names)));
    }
}
