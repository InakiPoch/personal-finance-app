using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetDebtSummary {
    public static async Task<Ok<DebtByPartyDto>> Handle(IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetDebtByPartyQuery(), cancellationToken);
        return TypedResults.Ok(response.ToDebtByPartyDto());
    }
}
