using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetPartyTimeline {
    public static async Task<Ok<PartyTimelineDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetPartyTimelineQuery(id), cancellationToken);
        return TypedResults.Ok(response.ToPartyTimelineDto());
    }
}
