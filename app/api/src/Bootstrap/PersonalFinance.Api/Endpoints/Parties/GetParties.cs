using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Helpers;
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
        var owed = await queryBus.AskAsync(new GetDebtByPartyQuery(), cancellationToken);
        var payableMonth = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var payable = await queryBus.AskAsync(new PayableBalancesAsOfQuery(payableMonth), cancellationToken);
        var scheduled = await queryBus.AskAsync(new GetPartyScheduledInstallmentsQuery(), cancellationToken);
        return TypedResults.Ok(PartiesListHelper.Build(roster, pending, owed, payable, scheduled));
    }
}
